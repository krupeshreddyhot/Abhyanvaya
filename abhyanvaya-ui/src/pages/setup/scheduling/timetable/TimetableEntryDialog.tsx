import { useEffect, useMemo, useState } from "react";
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  TextField,
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
  type CreateTimetableEntryRequest,
  type SubjectAllocationDto,
  type TimeSlotDto,
  type TimetableEntryDto,
} from "../../../../services/schedulingService";
import { DAY_LABELS, errMsg, parseOptionalSelectNumber, resolveSemestersForCourseGroup } from "../schedulingFormUtils";
import { periodTimeSlots } from "./timetableUtils";

export type TimetableEntryDialogProps = {
  open: boolean;
  timetableId: number;
  academicYearId: number;
  timeSlots: TimeSlotDto[];
  entry: TimetableEntryDto | null;
  initial?: Partial<CreateTimetableEntryRequest>;
  readOnly?: boolean;
  onClose: () => void;
  onSaved: (entry: TimetableEntryDto) => void;
  onDeleted?: (entryId: number) => void;
};

const TimetableEntryDialog = ({
  open,
  timetableId,
  academicYearId,
  timeSlots,
  entry,
  initial,
  readOnly = false,
  onClose,
  onSaved,
  onDeleted,
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

  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

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
    if (entry) {
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
    } else if (initial) {
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
  }, [open, entry, initial]);

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
    try {
      if (entry) {
        const res = await updateTimetableEntry(entry.id, { ...payload, id: entry.id });
        onSaved(res.data);
        onClose();
      } else {
        const res = await createTimetableEntry(timetableId, payload);
        onSaved(res.data);
        onClose();
      }
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    if (!entry || readOnly) return;
    setSaving(true);
    setError(null);
    try {
      await deleteTimetableEntry(entry.id);
      onDeleted?.(entry.id);
      onClose();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleDuplicate = async () => {
    if (!entry || readOnly) return;
    setSaving(true);
    setError(null);
    try {
      const res = await duplicateTimetableEntry(entry.id);
      onSaved(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleClone = async () => {
    if (!entry || readOnly || cloneDay === "" || cloneSlotId === "") {
      setError("Select target day and time slot to clone.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const res = await copyTimetableEntry(entry.id, {
        targetDayOfWeek: Number(cloneDay),
        targetTimeSlotId: Number(cloneSlotId),
        roomId: roomId === "" ? null : Number(roomId),
      });
      onSaved(res.data);
      onClose();
    } catch (e) {
      setError(errMsg(e));
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

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{entry ? "Edit timetable entry" : "New timetable entry"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          {error && <Alert severity="error">{error}</Alert>}

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel>Department</InputLabel>
            <Select
              label="Department"
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
            <InputLabel>Course</InputLabel>
            <Select
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
            <InputLabel>Group</InputLabel>
            <Select
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
            <InputLabel>Semester</InputLabel>
            <Select
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
            <InputLabel>Subject allocation</InputLabel>
            <Select
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

          <FormControl fullWidth disabled={readOnly}>
            <InputLabel>Faculty</InputLabel>
            <Select label="Faculty" value={staffId} onChange={(e) => setStaffId(parseOptionalSelectNumber(e.target.value))}>
              <MenuItem value="">From allocation</MenuItem>
              {staff.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel>Room</InputLabel>
            <Select label="Room" value={roomId} onChange={(e) => setRoomId(parseOptionalSelectNumber(e.target.value))}>
              <MenuItem value="">Select</MenuItem>
              {rooms.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel>Day</InputLabel>
            <Select label="Day" value={dayOfWeek} onChange={(e) => setDayOfWeek(parseOptionalSelectNumber(e.target.value))}>
              <MenuItem value="">Select</MenuItem>
              {days.map((d) => (
                <MenuItem key={d} value={d}>
                  {DAY_LABELS[d]}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          <FormControl fullWidth required disabled={readOnly}>
            <InputLabel>Time slot</InputLabel>
            <Select
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

          {entry && !readOnly && (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <FormControl sx={{ minWidth: 120 }}>
                <InputLabel>Clone day</InputLabel>
                <Select label="Clone day" value={cloneDay} onChange={(e) => setCloneDay(parseOptionalSelectNumber(e.target.value))}>
                  <MenuItem value="">—</MenuItem>
                  {days.map((d) => (
                    <MenuItem key={d} value={d}>
                      {DAY_LABELS[d]}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              <FormControl sx={{ minWidth: 140 }}>
                <InputLabel>Clone slot</InputLabel>
                <Select
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
        {entry && !readOnly && (
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
