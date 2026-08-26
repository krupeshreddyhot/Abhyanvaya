import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  MenuItem,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import {
  AcademicDataPanel,
  AcademicHelpHint,
  AcademicStatusChip,
  academicChipSx,
  academicPanelSx,
  academicTouchButtonSx,
} from "../academic";
import { getSubjects } from "../../services/attendanceService";
import { listSubjectAllocations } from "../../services/schedulingService";
import type { StaffListItem } from "../../services/setupService";
import {
  assignFacultySection,
  listFacultySections,
  listSectionGroups,
  type FacultySectionDto,
  type SectionDto,
  type SectionGroupDto,
} from "../../services/sectionService";
import {
  buildFacultySectionAllocationRows,
  type FacultyAllocationStatus,
} from "../../utils/facultySectionAllocationView";
import { facultyIdForAssign } from "../../utils/facultyStaffSelector";
import { FacultyStaffSelector } from "./FacultyStaffSelector";
import { useAcademicUi } from "../../context/AcademicUiContext";
import { isAbortError, replaceAbortController } from "../../utils/academicRequest";

import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import PermissionDeniedAlert from "../common/PermissionDeniedAlert";
import { AcademicPermissionAccess } from "../../auth/academicPermissionAccess";

const errMsg = (e: unknown): string => getApiErrorMessage(e, "Request failed.");

const statusColor = (status: FacultyAllocationStatus): "success" | "warning" | "default" => {
  if (status === "Current") return "success";
  if (status === "Ended") return "warning";
  return "default";
};

type Props = {
  academicYearId: number;
  courseId: number;
  groupId: number;
  semesterId: number;
  sections: SectionDto[];
  canAssignFaculty: boolean;
};

/**
 * Prompt 14 / 15A.6 — Faculty Allocation inside Section management.
 * Uses /faculty-sections + SubjectAllocations + SectionGroups + existing /staff selector.
 * No independent faculty-section model; assign payload still posts facultyId (Staff Id).
 */
export function FacultySectionAllocationPanel({
  academicYearId,
  courseId,
  groupId,
  semesterId,
  sections,
  canAssignFaculty,
}: Props) {
  const academicUi = useAcademicUi();
  const loadAbortRef = useRef<AbortController | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [assignments, setAssignments] = useState<FacultySectionDto[]>([]);
  const [sectionGroups, setSectionGroups] = useState<SectionGroupDto[]>([]);
  const [subjectNameById, setSubjectNameById] = useState<Map<number, string>>(() => new Map());
  const [subjectAllocations, setSubjectAllocations] = useState<
    {
      staffId: number;
      subjectId: number;
      academicYearId: number;
      courseId: number;
      groupId: number;
      semesterId: number;
    }[]
  >([]);

  const [filterSectionId, setFilterSectionId] = useState(0);
  const [selectedFaculty, setSelectedFaculty] = useState<StaffListItem | null>(null);
  const [facultySectionId, setFacultySectionId] = useState(0);
  const [facultyRole, setFacultyRole] = useState("Primary");
  const [effectiveFrom, setEffectiveFrom] = useState("");
  const selectedFacultyId = facultyIdForAssign(selectedFaculty);

  const load = useCallback(async () => {
    if (!academicYearId) {
      setAssignments([]);
      setSectionGroups([]);
      setSubjectAllocations([]);
      return;
    }
    const controller = replaceAbortController(loadAbortRef.current);
    loadAbortRef.current = controller;
    setLoading(true);
    setError(null);
    try {
      const sectionIdSet = new Set(sections.map((s) => s.id));
      const reuseSubjects =
        academicUi.selection.courseId === courseId &&
        academicUi.selection.groupId === groupId &&
        academicUi.selection.semesterId === semesterId &&
        academicUi.options.subjects.length > 0;

      const [fa, groups, allocs, subjects] = await Promise.all([
        // One faculty-sections call, then scope to visible sections — no N+1 per section / no full UI dump of unrelated scopes.
        listFacultySections({}, { signal: controller.signal }),
        listSectionGroups({
          academicYearId: academicYearId || undefined,
          semesterId: semesterId || undefined,
        }).catch(() => ({ data: [] as SectionGroupDto[] })),
        academicYearId
          ? listSubjectAllocations({ academicYearId }).catch(() => ({ data: [] }))
          : Promise.resolve({ data: [] }),
        reuseSubjects
          ? Promise.resolve({ data: academicUi.options.subjects })
          : courseId && groupId && semesterId
            ? getSubjects(courseId, groupId, semesterId, { signal: controller.signal }).catch(() => ({ data: [] }))
            : Promise.resolve({ data: [] }),
      ]);

      if (controller.signal.aborted) return;

      const scopedAssignments = (fa.data ?? []).filter((a) => sectionIdSet.size === 0 || sectionIdSet.has(a.sectionId));
      setAssignments(scopedAssignments);
      setSectionGroups(groups.data ?? []);

      const allocRows = (allocs.data ?? []).filter(
        (a) =>
          a.academicYearId === academicYearId &&
          (!courseId || a.courseId === courseId) &&
          (!groupId || a.groupId === groupId) &&
          (!semesterId || a.semesterId === semesterId),
      );
      setSubjectAllocations(
        allocRows.map((a) => ({
          staffId: a.staffId,
          subjectId: a.subjectId,
          academicYearId: a.academicYearId,
          courseId: a.courseId,
          groupId: a.groupId,
          semesterId: a.semesterId,
        })),
      );
      setSubjectNameById(new Map((subjects.data ?? []).map((s) => [s.id, s.name])));
    } catch (e) {
      if (isAbortError(e)) return;
      setError(errMsg(e));
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [
    academicYearId,
    courseId,
    groupId,
    semesterId,
    sections,
    academicUi.selection.courseId,
    academicUi.selection.groupId,
    academicUi.selection.semesterId,
    academicUi.options.subjects,
  ]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    if (sections[0] && facultySectionId <= 0) {
      setFacultySectionId(sections[0].id);
    }
  }, [sections, facultySectionId]);

  const displayRows = useMemo(
    () =>
      buildFacultySectionAllocationRows({
        assignments,
        sections: sections.map((s) => ({
          id: s.id,
          sectionCode: s.sectionCode,
          courseId: s.courseId,
          groupId: s.groupId,
          semesterId: s.semesterId,
          academicYearId: s.academicYearId,
        })),
        subjectAllocations,
        subjectNameById,
        sectionGroups,
        sectionFilterId: filterSectionId,
      }),
    [assignments, sections, subjectAllocations, subjectNameById, sectionGroups, filterSectionId],
  );

  const doAssign = async () => {
    if (!academicYearId || !facultySectionId || selectedFacultyId == null) return;
    setError(null);
    setMessage(null);
    try {
      await assignFacultySection({
        facultyId: selectedFacultyId,
        sectionId: facultySectionId,
        academicYearId,
        role: facultyRole,
        ...(effectiveFrom ? { effectiveFrom } : {}),
      });
      setMessage("Faculty assigned via existing faculty-sections API.");
      setSelectedFaculty(null);
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Stack spacing={1.25}>
      <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
          Faculty allocation
        </Typography>
        <AcademicHelpHint
          title="Faculty ↔ Section"
          body="Uses /api/faculty-sections. Subject column is enriched from Subject Allocations. Combined SectionGroup teaching shows as one operational class. Timetable attendance still uses StaffId + TimetableSections."
        />
      </Stack>

      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}
      {message && (
        <Alert severity="success" onClose={() => setMessage(null)}>
          {message}
        </Alert>
      )}

      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <AcademicStatusChip label={`Rows: ${displayRows.length}`} variant="outlined" />
        <AcademicStatusChip
          label={`Combined: ${displayRows.filter((r) => r.isCombinedSectionGroup).length}`}
          status="combined"
          variant="outlined"
        />
        <TextField
          select
          size="small"
          label="Filter Section"
          value={filterSectionId || ""}
          onChange={(e) => setFilterSectionId(Number(e.target.value) || 0)}
          sx={{ minWidth: { xs: "100%", sm: 160 } }}
        >
          <MenuItem value={0}>All sections</MenuItem>
          {sections.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.sectionCode}
            </MenuItem>
          ))}
        </TextField>
        <Button size="small" variant="outlined" onClick={() => void load()} disabled={loading} sx={academicTouchButtonSx}>
          Refresh
        </Button>
        {loading && <CircularProgress size={20} aria-label="Loading faculty allocations" />}
      </Stack>

      {!canAssignFaculty ? (
        <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.facultyAllocation.assign} />
      ) : (
        <Paper elevation={0} sx={{ ...academicPanelSx("academic"), bgcolor: "background.paper" }}>
          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 700, display: "block", mb: 1 }}>
            ASSIGN FACULTY
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "flex-start" }}>
            <FacultyStaffSelector value={selectedFaculty} onChange={setSelectedFaculty} />
            <TextField
              select
              size="small"
              label="Section"
              value={facultySectionId || ""}
              onChange={(e) => setFacultySectionId(Number(e.target.value))}
              sx={{ minWidth: { xs: "100%", sm: 140 } }}
            >
              {sections.map((s) => (
                <MenuItem key={s.id} value={s.id}>
                  {s.sectionCode}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              select
              size="small"
              label="Role"
              value={facultyRole}
              onChange={(e) => setFacultyRole(e.target.value)}
              sx={{ minWidth: { xs: "100%", sm: 120 } }}
            >
              <MenuItem value="Primary">Primary</MenuItem>
              <MenuItem value="Secondary">Secondary</MenuItem>
            </TextField>
            <TextField
              size="small"
              label="Effective From"
              type="date"
              value={effectiveFrom}
              onChange={(e) => setEffectiveFrom(e.target.value)}
              slotProps={{ inputLabel: { shrink: true } }}
              sx={{ minWidth: { xs: "100%", sm: 160 } }}
            />
            <Button
              variant="contained"
              size="small"
              onClick={() => void doAssign()}
              disabled={!academicYearId || !facultySectionId || selectedFacultyId == null}
              sx={{ ...academicTouchButtonSx, alignSelf: { sm: "center" } }}
            >
              Assign
            </Button>
          </Stack>
        </Paper>
      )}

      <AcademicDataPanel
        title="Assignments"
        accent="academic"
        loading={loading && displayRows.length === 0}
        loadingLabel="Loading faculty allocations…"
        empty={!loading && displayRows.length === 0}
        emptyTitle="No faculty allocations"
        emptyDescription="No faculty allocations for sections in this scope."
        helpTitle="Operational class"
        helpBody="Combined SectionGroup rows show underlying section codes and assignment IDs without inventing a new allocation model."
      >
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Operational Class</TableCell>
              <TableCell>Faculty</TableCell>
              <TableCell>Subject</TableCell>
              <TableCell>Effective From</TableCell>
              <TableCell>Effective To</TableCell>
              <TableCell>Allocation Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {displayRows.map((r) => (
              <TableRow key={r.key} hover>
                <TableCell>
                  <Stack spacing={0.5}>
                    <Typography variant="body2" sx={{ fontWeight: r.isCombinedSectionGroup ? 700 : 400 }}>
                      {r.operationalClassLabel}
                    </Typography>
                    <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5, alignItems: "center" }}>
                      <Typography variant="caption" color="text.secondary">
                        Underlying Sections:
                      </Typography>
                      {r.underlyingSectionCodes.map((code) => (
                        <Chip key={code} size="small" variant="outlined" label={code} sx={academicChipSx} />
                      ))}
                    </Box>
                    <Typography variant="caption" color="text.secondary">
                      {r.isCombinedSectionGroup && r.sectionGroupCode
                        ? `SectionGroup ${r.sectionGroupCode} · ${r.role}`
                        : r.role}
                      {" · "}
                      Assignment IDs: {r.assignmentIds.join(", ")}
                    </Typography>
                  </Stack>
                </TableCell>
                <TableCell>
                  <Stack spacing={0.25}>
                    <Typography variant="body2">{r.facultyName || "Faculty"}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      Staff ID #{r.facultyId}
                    </Typography>
                  </Stack>
                </TableCell>
                <TableCell>{r.subjectLabel}</TableCell>
                <TableCell>{r.effectiveFrom || "—"}</TableCell>
                <TableCell>{r.effectiveTo || "—"}</TableCell>
                <TableCell>
                  <AcademicStatusChip
                    label={r.allocationStatus}
                    status={r.allocationStatus}
                    color={statusColor(r.allocationStatus)}
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </AcademicDataPanel>
    </Stack>
  );
}
