import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormHelperText,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {
  listDepartments,
  listMasterCourses,
  listMasterGroups,
  listSemesters,
  listStaff,
  listSubjectCatalog,
} from "../../../../services/setupService";
import {
  copyTimetableEntry,
  createTimetableEntry,
  deleteTimetableEntry,
  duplicateTimetableEntry,
  listSubjectAllocations,
  searchRooms,
  updateTimetableEntry,
  type CompatibleTeachingGroupOptionDto,
  type CreateTimetableEntryRequest,
  type SoftWarningDto,
  type SubjectAllocationDto,
  type TimeSlotDto,
  type TimetableEntryDto,
} from "../../../../services/schedulingService";
import { TeachingGroupStatus } from "../../../../services/teachingGroupService";
import { getApiErrorMessage } from "../../../../utils/apiErrorMessage";
import { DAY_LABELS, errMsg, parseOptionalSelectNumber, resolveSemestersForCourseGroup } from "../schedulingFormUtils";
import {
  formatCapacityDisplay,
  formatTeachingGroupSelectorOptionLabel,
  teachingGroupStatusLabel,
} from "../teachingGroupUi";
import { entryCapacityFeedbackFromSoftWarnings, periodTimeSlots } from "./timetableUtils";
import {
  applyTeachingGroupSelectionDelta,
  reloadCompatibleTeachingGroups,
} from "./timetableTeachingGroupAssignmentActions";

export type TimetableEntryDialogProps = {
  open: boolean;
  timetableId: number;
  academicYearId: number;
  timeSlots: TimeSlotDto[];
  entry: TimetableEntryDto | null;
  initial?: Partial<CreateTimetableEntryRequest>;
  readOnly?: boolean;
  /** Server soft warnings for presentation (AI-SCHED-CAP Prompt 4). */
  softWarnings?: SoftWarningDto[];
  onClose: () => void;
  onSaved: (entry: TimetableEntryDto) => void;
  onDeleted?: (entryId: number) => void;
  /** Forward compatible-TG rows for grid display hints (informational only). */
  onTeachingGroupOptionsLoaded?: (options: CompatibleTeachingGroupOptionDto[]) => void;
  /** After HTTP 409: parent should refresh grid from server (no auto-retry). */
  onTeachingGroupConflict?: () => void | Promise<void>;
};

const TimetableEntryDialog = ({
  open,
  timetableId,
  academicYearId,
  timeSlots,
  entry,
  initial,
  readOnly = false,
  softWarnings,
  onClose,
  onSaved,
  onDeleted,
  onTeachingGroupOptionsLoaded,
  onTeachingGroupConflict,
}: TimetableEntryDialogProps) => {
  const periodSlots = useMemo(() => periodTimeSlots(timeSlots), [timeSlots]);
  const days = useMemo(() => [1, 2, 3, 4, 5, 6, 0], []);

  const [departments, setDepartments] = useState<{ id: number; name: string }[]>([]);
  const [allocations, setAllocations] = useState<SubjectAllocationDto[]>([]);
  const [courses, setCourses] = useState<{ id: number; name: string }[]>([]);
  const [groups, setGroups] = useState<{ id: number; name: string; courseId: number }[]>([]);
  const [semesters, setSemesters] = useState<{ id: number; name: string; courseId: number; groupId: number | null }[]>([]);
  const [staff, setStaff] = useState<{ id: number; label: string }[]>([]);
  const [rooms, setRooms] = useState<{ id: number; label: string }[]>([]);
  const [subjectNameById, setSubjectNameById] = useState<Map<number, string>>(new Map());

  const [departmentId, setDepartmentId] = useState<number | "">("");
  const [courseId, setCourseId] = useState<number | "">("");
  const [groupId, setGroupId] = useState<number | "">("");
  const [semesterId, setSemesterId] = useState<number | "">("");
  const [allocationId, setAllocationId] = useState<number | "">("");
  const [staffId, setStaffId] = useState<number | "">("");
  const [roomId, setRoomId] = useState<number | "">("");
  const [dayOfWeek, setDayOfWeek] = useState<number | "">("");
  const [timeSlotId, setTimeSlotId] = useState<number | "">("");
  const [remarks, setRemarks] = useState("");

  const [cloneDay, setCloneDay] = useState<number | "">("");
  const [cloneSlotId, setCloneSlotId] = useState<number | "">("");

  const [workingEntry, setWorkingEntry] = useState<TimetableEntryDto | null>(null);
  const [compatibleTgs, setCompatibleTgs] = useState<CompatibleTeachingGroupOptionDto[]>([]);
  const [tgLoading, setTgLoading] = useState(false);
  const [selectedTeachingGroupId, setSelectedTeachingGroupId] = useState<number | "">("");
  const [baselineTeachingGroupId, setBaselineTeachingGroupId] = useState<number | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);

  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const loadCompatible = useCallback(async (entryId: number, baseEntry: TimetableEntryDto) => {
    setTgLoading(true);
    try {
      const reloaded = await reloadCompatibleTeachingGroups(entryId, baseEntry);
      setCompatibleTgs(reloaded.options);
      setWorkingEntry(reloaded.entry);
      const assigned = reloaded.entry.teachingGroupId ?? null;
      setBaselineTeachingGroupId(assigned);
      setSelectedTeachingGroupId(assigned ?? "");
      onTeachingGroupOptionsLoaded?.(reloaded.options);
      return reloaded;
    } catch (e) {
      setCompatibleTgs([]);
      setError(getApiErrorMessage(e, errMsg(e)));
      return null;
    } finally {
      setTgLoading(false);
    }
  }, [onTeachingGroupOptionsLoaded]);

  useEffect(() => {
    if (!open) return;
    void (async () => {
      try {
        const [c, g, sem, st, roomRes, allocRes, deptRes, subjectRes] = await Promise.all([
          listMasterCourses(),
          listMasterGroups(),
          listSemesters(),
          listStaff({ page: 1, pageSize: 500 }),
          searchRooms({ page: 1, pageSize: 500, isActive: true }),
          listSubjectAllocations({ academicYearId }),
          listDepartments(undefined, true),
          listSubjectCatalog(),
        ]);
        setCourses(c.data.map((x) => ({ id: x.id, name: x.name })));
        setGroups(g.data.map((x) => ({ id: x.id, name: x.name, courseId: x.courseId })));
        setSemesters(sem.data.map((x) => ({ id: x.id, name: x.name, courseId: x.courseId, groupId: x.groupId })));
        setStaff(st.data.items.map((s) => ({ id: s.id, label: `${s.firstName} ${s.lastName}` })));
        setRooms(roomRes.data.items.map((r) => ({ id: r.id, label: `${r.code} — ${r.name}` })));
        setAllocations(allocRes.data);
        setDepartments(deptRes.data.map((d) => ({ id: d.id, name: d.name })));
        setSubjectNameById(new Map(subjectRes.data.map((s) => [s.id, s.name])));
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, [open, academicYearId]);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setInfoMessage(null);
    if (entry) {
      setWorkingEntry(entry);
      setDepartmentId(entry.departmentId);
      setCourseId(entry.courseId);
      setGroupId(entry.groupId);
      setSemesterId(entry.semesterId);
      setAllocationId(entry.subjectAllocationId);
      setStaffId(entry.staffId);
      setRoomId(entry.roomId);
      setDayOfWeek(entry.dayOfWeek);
      setTimeSlotId(entry.timeSlotId);
      setRemarks(entry.remarks ?? "");
      const assigned = entry.teachingGroupId ?? null;
      setBaselineTeachingGroupId(assigned);
      setSelectedTeachingGroupId(assigned ?? "");
      void loadCompatible(entry.id, entry);
    } else if (initial) {
      setWorkingEntry(null);
      setCompatibleTgs([]);
      setBaselineTeachingGroupId(null);
      setSelectedTeachingGroupId("");
      setDayOfWeek(initial.dayOfWeek ?? "");
      setTimeSlotId(initial.timeSlotId ?? "");
      setAllocationId(initial.subjectAllocationId ?? "");
      setRoomId(initial.roomId ?? "");
      setRemarks(initial.remarks ?? "");
      setDepartmentId("");
      setCourseId("");
      setGroupId("");
      setSemesterId("");
      setStaffId("");
    } else {
      setWorkingEntry(null);
      setCompatibleTgs([]);
      setBaselineTeachingGroupId(null);
      setSelectedTeachingGroupId("");
      setDepartmentId("");
      setCourseId("");
      setGroupId("");
      setSemesterId("");
      setAllocationId("");
      setStaffId("");
      setRoomId("");
      setDayOfWeek("");
      setTimeSlotId("");
      setRemarks("");
    }
    setCloneDay("");
    setCloneSlotId("");
  }, [open, entry, initial, loadCompatible]);

  const filteredAllocations = useMemo(() => {
    return allocations.filter((a) => {
      if (departmentId !== "" && a.departmentId !== departmentId) return false;
      if (courseId !== "" && a.courseId !== courseId) return false;
      if (groupId !== "" && a.groupId !== groupId) return false;
      if (semesterId !== "" && a.semesterId !== semesterId) return false;
      return true;
    });
  }, [allocations, departmentId, courseId, groupId, semesterId]);

  const selectedAllocation = allocations.find((a) => a.id === allocationId);

  useEffect(() => {
    if (selectedAllocation && staffId === "") {
      setStaffId(selectedAllocation.staffId);
      if (roomId === "" && selectedAllocation.preferredRoomId) {
        setRoomId(selectedAllocation.preferredRoomId);
      }
    }
  }, [selectedAllocation, staffId, roomId]);

  const selectedTgOption = useMemo(
    () =>
      selectedTeachingGroupId === ""
        ? null
        : compatibleTgs.find((t) => t.id === selectedTeachingGroupId) ?? null,
    [compatibleTgs, selectedTeachingGroupId],
  );

  const capacityWarning =
    selectedTgOption != null && Boolean(selectedTgOption.isOverMaxTeachingCapacity);

  const entrySoftCapacityFeedback = useMemo(
    () => (entry ? entryCapacityFeedbackFromSoftWarnings(entry.id, softWarnings) : []),
    [entry, softWarnings],
  );

  const buildPayload = (): CreateTimetableEntryRequest | null => {
    if (
      dayOfWeek === "" ||
      timeSlotId === "" ||
      allocationId === "" ||
      roomId === ""
    ) {
      setError("Complete all required fields.");
      return null;
    }
    return {
      dayOfWeek: Number(dayOfWeek),
      timeSlotId: Number(timeSlotId),
      subjectAllocationId: Number(allocationId),
      roomId: Number(roomId),
      remarks: remarks.trim() || null,
    };
  };

  const handleSave = async () => {
    const payload = buildPayload();
    if (!payload) return;
    setSaving(true);
    setError(null);
    setInfoMessage(null);
    try {
      let current = workingEntry;

      if (!current) {
        // Create without TeachingGroupId — dedicated assign runs after an id exists.
        const created = await createTimetableEntry(timetableId, payload);
        current = created.data;
        setWorkingEntry(current);
        onSaved(current);
        setBaselineTeachingGroupId(null);
        setSelectedTeachingGroupId("");
        await loadCompatible(current.id, current);
        setInfoMessage(
          "Timetable entry created. Select a Teaching Group below if needed, then save again — or close to leave it unassigned.",
        );
        return;
      }

      const updated = await updateTimetableEntry(current.id, { ...payload, id: current.id });
      current = updated.data;
      setWorkingEntry(current);

      const tgOutcome = await applyTeachingGroupSelectionDelta(
        current,
        selectedTeachingGroupId,
        baselineTeachingGroupId,
      );

      if (tgOutcome.kind === "conflict") {
        setWorkingEntry(tgOutcome.entry);
        setCompatibleTgs(tgOutcome.options);
        const assigned = tgOutcome.entry.teachingGroupId ?? null;
        setBaselineTeachingGroupId(assigned);
        setSelectedTeachingGroupId(assigned ?? "");
        onTeachingGroupOptionsLoaded?.(tgOutcome.options);
        onSaved(tgOutcome.entry);
        await onTeachingGroupConflict?.();
        setError(tgOutcome.message);
        return;
      }
      if (tgOutcome.kind === "error") {
        setError(tgOutcome.message);
        onSaved(current);
        return;
      }

      const finalEntry = tgOutcome.entry;
      setWorkingEntry(finalEntry);
      onSaved(finalEntry);
      await loadCompatible(finalEntry.id, finalEntry);
      onClose();
    } catch (e) {
      setError(getApiErrorMessage(e, errMsg(e)));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!workingEntry || readOnly) return;
    setSaving(true);
    setError(null);
    try {
      await deleteTimetableEntry(workingEntry.id);
      onDeleted?.(workingEntry.id);
      onClose();
    } catch (e) {
      setError(getApiErrorMessage(e, errMsg(e)));
    } finally {
      setSaving(false);
    }
  };

  const handleDuplicate = async () => {
    if (!workingEntry || readOnly) return;
    setSaving(true);
    setError(null);
    try {
      const res = await duplicateTimetableEntry(workingEntry.id);
      onSaved(res.data);
    } catch (e) {
      setError(getApiErrorMessage(e, errMsg(e)));
    } finally {
      setSaving(false);
    }
  };

  const handleClone = async () => {
    if (!workingEntry || readOnly || cloneDay === "" || cloneSlotId === "") {
      setError("Select target day and time slot to clone.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const res = await copyTimetableEntry(workingEntry.id, {
        targetDayOfWeek: Number(cloneDay),
        targetTimeSlotId: Number(cloneSlotId),
        roomId: roomId === "" ? null : Number(roomId),
      });
      onSaved(res.data);
      onClose();
    } catch (e) {
      setError(getApiErrorMessage(e, errMsg(e)));
    } finally {
      setSaving(false);
    }
  };

  const onAllocationChange = (id: number | "") => {
    setAllocationId(id);
    const alloc = allocations.find((a) => a.id === id);
    if (alloc) {
      setDepartmentId(alloc.departmentId);
      setCourseId(alloc.courseId);
      setGroupId(alloc.groupId);
      setSemesterId(alloc.semesterId);
      setStaffId(alloc.staffId);
      if (alloc.preferredRoomId) setRoomId(alloc.preferredRoomId);
    }
  };

  const dialogTitle = workingEntry ? "Edit timetable entry" : "New timetable entry";
  const canEditTg = !readOnly && workingEntry != null;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth aria-labelledby="timetable-entry-dialog-title">
      <DialogTitle id="timetable-entry-dialog-title">{dialogTitle}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && (
            <Alert severity="error" role="alert">
              {error}
            </Alert>
          )}
          {infoMessage && (
            <Alert severity="success" role="status" onClose={() => setInfoMessage(null)}>
              {infoMessage}
            </Alert>
          )}

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel id="tt-entry-dept">Department (filter)</InputLabel>
            <Select
              labelId="tt-entry-dept"
              label="Department (filter)"
              value={departmentId}
              onChange={(e) => setDepartmentId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">All</MenuItem>
              {departments.map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel id="tt-entry-course">Course</InputLabel>
            <Select
              labelId="tt-entry-course"
              label="Course"
              value={courseId}
              onChange={(e) => setCourseId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">All</MenuItem>
              {courses.map((c) => (
                <MenuItem key={c.id} value={c.id}>
                  {c.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel id="tt-entry-group">Group</InputLabel>
            <Select
              labelId="tt-entry-group"
              label="Group"
              value={groupId}
              onChange={(e) => setGroupId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">All</MenuItem>
              {groups
                .filter((g) => courseId === "" || g.courseId === courseId)
                .map((g) => (
                  <MenuItem key={g.id} value={g.id}>
                    {g.name}
                  </MenuItem>
                ))}
            </Select>
          </FormControl>

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel id="tt-entry-sem">Semester</InputLabel>
            <Select
              labelId="tt-entry-sem"
              label="Semester"
              value={semesterId}
              onChange={(e) => setSemesterId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">All</MenuItem>
              {resolveSemestersForCourseGroup(semesters, courseId, groupId, {
                selectedSemesterId: semesterId === "" ? null : semesterId,
              }).map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel id="tt-entry-sa">Subject allocation</InputLabel>
            <Select
              labelId="tt-entry-sa"
              label="Subject allocation"
              value={allocationId}
              onChange={(e) => onAllocationChange(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select</MenuItem>
              {filteredAllocations.map((a) => {
                const subject = subjectNameById.get(a.subjectId) ?? `Subject #${a.subjectId}`;
                const faculty = staff.find((s) => s.id === a.staffId)?.label ?? `Staff #${a.staffId}`;
                return (
                  <MenuItem key={a.id} value={a.id}>
                    {subject} — {faculty} ({a.weeklyHours}h/wk)
                  </MenuItem>
                );
              })}
            </Select>
          </FormControl>

          <FormControl fullWidth disabled={!canEditTg || tgLoading || saving}>
            <InputLabel id="tt-entry-tg">Teaching Group</InputLabel>
            <Select
              labelId="tt-entry-tg"
              label="Teaching Group"
              value={selectedTeachingGroupId}
              onChange={(e) => setSelectedTeachingGroupId(parseOptionalSelectNumber(e.target.value))}
              slotProps={{
                input: { "aria-label": "Teaching Group" },
              }}
            >
              <MenuItem value="">
                <em>No Teaching Group</em>
              </MenuItem>
              {compatibleTgs.map((tg) => (
                <MenuItem key={tg.id} value={tg.id}>
                  {formatTeachingGroupSelectorOptionLabel(tg)}
                </MenuItem>
              ))}
            </Select>
            <FormHelperText>
              {!workingEntry
                ? "Save the entry first to load compatible Teaching Groups and assign one."
                : readOnly
                  ? "View only — Teaching Group assignment requires timetable manage permission and a draft timetable."
                  : "Assignment uses a dedicated server API. Compatibility is determined by the server."}
            </FormHelperText>
          </FormControl>

          {workingEntry && tgLoading && (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }} role="status" aria-live="polite">
              <CircularProgress size={18} />
              <Typography variant="body2">Loading compatible Teaching Groups…</Typography>
            </Stack>
          )}

          {workingEntry && !tgLoading && compatibleTgs.length === 0 && (
            <Alert severity="info" variant="outlined" role="status">
              No compatible Teaching Groups are available for this timetable entry.
            </Alert>
          )}

          {selectedTgOption?.status === TeachingGroupStatus.Archived && (
            <Alert severity="warning" role="status">
              Current Teaching Group is archived ({teachingGroupStatusLabel(selectedTgOption.status)}
              {selectedTgOption.code ? ` — ${selectedTgOption.code}` : ""}). It remains assigned until you
              explicitly clear it or choose another compatible group.
            </Alert>
          )}

          {capacityWarning && selectedTgOption && (
            <Alert severity="warning" role="status">
              Teaching Group capacity exceeded (server): {selectedTgOption.resolvedStudentCount} resolved students
              vs maximum Teaching Group capacity of {formatCapacityDisplay(selectedTgOption.maxTeachingCapacity)}.
              Room capacity is evaluated separately by scheduling validation.
            </Alert>
          )}
          {entrySoftCapacityFeedback.map((w) => (
            <Alert key={w.code} severity="warning" role="status">
              <Typography variant="subtitle2">{w.title ?? w.code}</Typography>
              {w.why && (
                <Typography variant="body2" sx={{ mt: 0.5 }}>
                  {w.why}
                </Typography>
              )}
              {w.suggestedAction && (
                <Typography variant="body2" sx={{ mt: 0.5 }}>
                  {w.suggestedAction}
                </Typography>
              )}
            </Alert>
          ))}

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel id="tt-entry-staff">Faculty</InputLabel>
            <Select
              labelId="tt-entry-staff"
              label="Faculty"
              value={staffId}
              onChange={(e) => setStaffId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">From allocation</MenuItem>
              {staff.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel id="tt-entry-room">Room</InputLabel>
            <Select
              labelId="tt-entry-room"
              label="Room"
              value={roomId}
              onChange={(e) => setRoomId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select</MenuItem>
              {rooms.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel id="tt-entry-day">Day</InputLabel>
            <Select
              labelId="tt-entry-day"
              label="Day"
              value={dayOfWeek}
              onChange={(e) => setDayOfWeek(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select</MenuItem>
              {days.map((d) => (
                <MenuItem key={d} value={d}>
                  {DAY_LABELS[d]}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel id="tt-entry-slot">Time slot</InputLabel>
            <Select
              labelId="tt-entry-slot"
              label="Time slot"
              value={timeSlotId}
              onChange={(e) => setTimeSlotId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select</MenuItem>
              {periodSlots.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.name} ({s.startTime})
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <TextField
            label="Remarks"
            value={remarks}
            onChange={(e) => setRemarks(e.target.value)}
            fullWidth
            multiline
            minRows={2}
            disabled={readOnly}
          />

          {workingEntry && !readOnly && (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }} useFlexGap>
              <FormControl sx={{ minWidth: 120 }}>
                <InputLabel id="tt-clone-day">Clone day</InputLabel>
                <Select
                  labelId="tt-clone-day"
                  label="Clone day"
                  value={cloneDay}
                  onChange={(e) => setCloneDay(parseOptionalSelectNumber(e.target.value))}
                >
                  <MenuItem value="">—</MenuItem>
                  {days.map((d) => (
                    <MenuItem key={d} value={d}>
                      {DAY_LABELS[d]}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <FormControl sx={{ minWidth: 140 }}>
                <InputLabel id="tt-clone-slot">Clone slot</InputLabel>
                <Select
                  labelId="tt-clone-slot"
                  label="Clone slot"
                  value={cloneSlotId}
                  onChange={(e) => setCloneSlotId(parseOptionalSelectNumber(e.target.value))}
                >
                  <MenuItem value="">—</MenuItem>
                  {periodSlots.map((s) => (
                    <MenuItem key={s.id} value={s.id}>
                      {s.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <Button onClick={() => void handleClone()} disabled={saving}>
                Clone
              </Button>
            </Stack>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        {workingEntry && !readOnly && (
          <>
            <Button color="error" onClick={() => void handleDelete()} disabled={saving}>
              Delete
            </Button>
            <Button onClick={() => void handleDuplicate()} disabled={saving}>
              Duplicate
            </Button>
          </>
        )}
        <Button onClick={onClose}>Cancel</Button>
        {!readOnly && (
          <Button variant="contained" onClick={() => void handleSave()} disabled={saving}>
            Save
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
};

export default TimetableEntryDialog;
