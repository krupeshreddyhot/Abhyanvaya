import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  MenuItem,
  Stack,
  Tab,
  Tabs,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { AcademicPermissionAccess } from "../../auth/academicPermissionAccess";
import { PermissionKeys } from "../../auth/permissionKeys";
import { useAuth } from "../../context/AuthContext";
import { useAcademicUi } from "../../context/AcademicUiContext";
import {
  AcademicConfirmDialog,
  AcademicContextBreadcrumb,
  AcademicDataPanel,
  AcademicOperationalPageShell,
  AcademicScopeSelector,
  AcademicScopeToolbar,
  AcademicStatusChip,
  academicTouchButtonSx,
} from "../../components/academic";
import { EnterpriseAllocationWorkspace } from "../../components/allocation";
import PermissionAwareButton from "../../components/common/PermissionAwareButton";
import PermissionDeniedAlert from "../../components/common/PermissionDeniedAlert";
import { FacultySectionAllocationPanel } from "../../components/sections/FacultySectionAllocationPanel";
import { isAcademicScopeReady } from "../../utils/academicSelectorFieldState";
import { getApiErrorMessage } from "../../utils/apiErrorMessage";
import {
  autoAllocateSections,
  commitMerge,
  commitSplit,
  createSection,
  deleteSection,
  exportSectionReport,
  getCapacitySummary,
  getLifecycleHistory,
  getMergeHistory,
  getSectionOccupancy,
  getSplitHistory,
  listLifecycleStates,
  listSectionReadiness,
  listSections,
  previewMerge,
  previewSplit,
  transferStudentSection,
  transitionSectionLifecycle,
  updateSection,
  getSectionStatistics,
  type SectionCapacitySnapshotDto,
  type SectionDto,
  type SectionMergeHistoryDto,
  type SectionMergePreviewDto,
  type SectionReadinessDto,
  type SectionSplitHistoryDto,
  type SectionSplitPreviewDto,
  type SectionStatisticsDto,
} from "../../services/sectionService";

const errMsg = (e: unknown): string => getApiErrorMessage(e, "Request failed.");

const formatDate = (value?: string | null) => {
  if (!value) return "—";
  return value.length >= 10 ? value.slice(0, 10) : value;
};

const readinessColor = (status?: string): "default" | "success" | "warning" | "error" | "info" => {
  const s = (status ?? "").toLowerCase();
  if (s.includes("ready") || s.includes("healthy")) return "success";
  if (s.includes("warn")) return "warning";
  if (s.includes("block") || s.includes("critical")) return "error";
  return "default";
};

const TAB = {
  List: 0,
  Students: 1,
  Faculty: 2,
  Transfer: 3,
  Lifecycle: 4,
  Capacity: 5,
  Merge: 6,
  Split: 7,
  Readiness: 8,
  History: 9,
} as const;

/**
 * AI29.1D — Sections operational UI.
 * Consumes existing Section / lifecycle / capacity APIs via AcademicScopeSelector.
 * No new section business logic in React.
 */
const SectionsPage = () => {
  const { hasPermission, hasAnyPermission } = useAuth();
  const canView = hasAnyPermission([...AcademicPermissionAccess.sections.routeAny]);
  const canCreate = hasPermission(PermissionKeys.SectionCreate);
  const canEdit = hasPermission(PermissionKeys.SectionEdit);
  const canDelete = hasPermission(PermissionKeys.SectionDelete);
  const canAssignStudents = hasPermission(PermissionKeys.SectionAssignStudents);
  const canAssignFaculty = hasPermission(PermissionKeys.SectionAssignFaculty);
  const canLifecycleView = hasPermission(PermissionKeys.SectionLifecycleView);
  const canLifecycleEdit = hasPermission(PermissionKeys.SectionLifecycleEdit);
  const canCapacity = hasPermission(PermissionKeys.SectionCapacity);
  const canMerge = hasPermission(PermissionKeys.SectionMerge);
  const canSplit = hasPermission(PermissionKeys.SectionSplit);
  const canReadiness = hasPermission(PermissionKeys.SectionReadiness);

  const { selection, catalogs } = useAcademicUi();
  const yearId = selection.academicYearId ?? 0;
  const courseId = selection.courseId ?? 0;
  const groupId = selection.groupId ?? 0;
  const semesterId = selection.semesterId ?? 0;
  const scopeReady = isAcademicScopeReady(selection);

  const [tab, setTab] = useState(0);
  const [rows, setRows] = useState<SectionDto[]>([]);
  const [statsBySection, setStatsBySection] = useState<Record<number, SectionStatisticsDto>>({});
  const [readinessBySection, setReadinessBySection] = useState<Record<number, SectionReadinessDto>>({});
  const [effectiveBySection, setEffectiveBySection] = useState<Record<number, string>>({});
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [deleteTargetId, setDeleteTargetId] = useState<number | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [maxStrength, setMaxStrength] = useState(60);
  const [displayOrder, setDisplayOrder] = useState(0);
  const [sectionTypeCode, setSectionTypeCode] = useState("Regular");

  const [transferStudentId, setTransferStudentId] = useState("");
  const [transferSectionId, setTransferSectionId] = useState(0);
  const [transferReason, setTransferReason] = useState("");
  const [strategy, setStrategy] = useState("Alphabetical");

  const [lifecycleStates, setLifecycleStates] = useState<string[]>([]);
  const [lifecycleSectionId, setLifecycleSectionId] = useState(0);
  const [lifecycleTarget, setLifecycleTarget] = useState("Active");
  const [lifecycleReason, setLifecycleReason] = useState("");
  const [lifecycleHistory, setLifecycleHistory] = useState<
    { fromStatus: string; toStatus: string; reason?: string; transitionedUtc: string }[]
  >([]);
  const [occupancy, setOccupancy] = useState<SectionCapacitySnapshotDto[]>([]);
  const [capacitySummary, setCapacitySummary] = useState("");
  const [health, setHealth] = useState<SectionReadinessDto[]>([]);
  const [mergeSourceIds, setMergeSourceIds] = useState<number[]>([]);
  const [mergeTarget, setMergeTarget] = useState(0);
  const [mergePreview, setMergePreview] = useState<SectionMergePreviewDto | null>(null);
  const [splitSource, setSplitSource] = useState(0);
  const [splitPreview, setSplitPreview] = useState<SectionSplitPreviewDto | null>(null);
  const [mergeHistory, setMergeHistory] = useState<SectionMergeHistoryDto[]>([]);
  const [splitHistory, setSplitHistory] = useState<SectionSplitHistoryDto[]>([]);

  const yearStartDate = useMemo(() => {
    const y = catalogs.academicYears.find((a) => a.id === yearId);
    return y?.startDate ? formatDate(y.startDate) : null;
  }, [catalogs.academicYears, yearId]);

  const loadSections = useCallback(async () => {
    if (!yearId) {
      setRows([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const res = await listSections({
        academicYearId: yearId || undefined,
        courseId: courseId || undefined,
        groupId: groupId || undefined,
        semesterId: semesterId || undefined,
      });
      const sections = res.data ?? [];
      setRows(sections);
      if (sections[0]) {
        setTransferSectionId((prev) => prev || sections[0]!.id);
        setLifecycleSectionId((prev) => prev || sections[0]!.id);
        setMergeTarget((prev) => prev || sections[0]!.id);
        setSplitSource((prev) => prev || sections[0]!.id);
      }

      const [statsRes, readinessRes] = await Promise.all([
        getSectionStatistics({
          academicYearId: yearId || undefined,
          semesterId: semesterId || undefined,
        }).catch(() => null),
        canReadiness
          ? listSectionReadiness({
              academicYearId: yearId || undefined,
              semesterId: semesterId || undefined,
            }).catch(() => null)
          : Promise.resolve(null),
      ]);

      const statsMap: Record<number, SectionStatisticsDto> = {};
      for (const s of statsRes?.data ?? []) statsMap[s.sectionId] = s;
      setStatsBySection(statsMap);

      const readyMap: Record<number, SectionReadinessDto> = {};
      for (const h of readinessRes?.data ?? []) readyMap[h.sectionId] = h;
      setReadinessBySection(readyMap);
      if (readinessRes?.data) setHealth(readinessRes.data);

      // Prompt 19 — avoid N+1 getSectionVersions per row. Year start is the list default;
      // version history remains available via lifecycle/ops actions for a selected section.
      const effectiveMap: Record<number, string> = {};
      if (yearStartDate) {
        for (const s of sections) effectiveMap[s.id] = yearStartDate;
      }
      setEffectiveBySection(effectiveMap);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [yearId, courseId, groupId, semesterId, canReadiness, yearStartDate]);

  const loadOps = useCallback(async () => {
    try {
      if (canCapacity) {
        const [occ, sum] = await Promise.all([
          getSectionOccupancy({ academicYearId: yearId || undefined, semesterId: semesterId || undefined }),
          getCapacitySummary({ academicYearId: yearId || undefined, semesterId: semesterId || undefined }),
        ]);
        setOccupancy(occ.data ?? []);
        setCapacitySummary(
          `${sum.data.sectionCount} sections · avg occupancy ${sum.data.averageOccupancyPercent}% · over ${sum.data.overCapacityCount} · under ${sum.data.underCapacityCount}`,
        );
      }
      if (canReadiness) {
        const h = await listSectionReadiness({
          academicYearId: yearId || undefined,
          semesterId: semesterId || undefined,
        });
        setHealth(h.data ?? []);
        const readyMap: Record<number, SectionReadinessDto> = {};
        for (const row of h.data ?? []) readyMap[row.sectionId] = row;
        setReadinessBySection(readyMap);
      }
      if (canLifecycleView || canLifecycleEdit) {
        const states = await listLifecycleStates();
        setLifecycleStates(states.data ?? []);
      }
    } catch (e) {
      setError(errMsg(e));
    }
  }, [canCapacity, canReadiness, canLifecycleView, canLifecycleEdit, yearId, semesterId]);

  useEffect(() => {
    void loadSections();
  }, [loadSections]);

  useEffect(() => {
    if (tab >= TAB.Lifecycle) void loadOps();
  }, [tab, loadOps]);

  const openAdd = () => {
    setEditingId(0);
    setCode("");
    setName("");
    setMaxStrength(60);
    setDisplayOrder(rows.length);
    setSectionTypeCode("Regular");
    setDialogOpen(true);
  };

  const openEdit = (r: SectionDto) => {
    setEditingId(r.id);
    setCode(r.sectionCode);
    setName(r.sectionName);
    setMaxStrength(r.maximumStrength);
    setDisplayOrder(r.displayOrder);
    setSectionTypeCode(r.sectionTypeCode || "Regular");
    setDialogOpen(true);
  };

  const save = async () => {
    if (!scopeReady) {
      setError("Select Academic Year, Course, Group, and Semester before creating a section.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) {
        await updateSection(editingId, {
          sectionCode: code,
          sectionName: name,
          displayOrder,
          maximumStrength: maxStrength,
          status: "Active",
          sectionTypeCode,
        });
        setMessage("Section updated.");
      } else {
        await createSection({
          academicYearId: yearId,
          courseId,
          groupId,
          semesterId,
          sectionCode: code,
          sectionName: name,
          displayOrder,
          maximumStrength: maxStrength,
          status: "Active",
          sectionTypeCode,
        });
        setMessage("Section created.");
      }
      setDialogOpen(false);
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id: number) => {
    setDeleting(true);
    try {
      await deleteSection(id);
      setMessage("Section deleted.");
      setDeleteTargetId(null);
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setDeleting(false);
    }
  };

  const doTransfer = async () => {
    try {
      await transferStudentSection({
        studentId: Number(transferStudentId),
        targetSectionId: transferSectionId,
        reason: transferReason || undefined,
      });
      setMessage("Student transferred (history preserved).");
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doAutoAllocate = async () => {
    if (!scopeReady) return;
    try {
      const res = await autoAllocateSections({
        academicYearId: yearId,
        courseId,
        groupId,
        semesterId,
        strategy,
      });
      setMessage((res.data.messages ?? []).join(" ") || `Assigned ${res.data.assignedCount}, skipped ${res.data.skippedCount}.`);
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doTransition = async () => {
    try {
      await transitionSectionLifecycle(lifecycleSectionId, {
        targetStatus: lifecycleTarget,
        reason: lifecycleReason || undefined,
      });
      setMessage("Lifecycle transition applied.");
      const hist = await getLifecycleHistory(lifecycleSectionId);
      setLifecycleHistory(hist.data ?? []);
      await loadSections();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doMergePreview = async () => {
    try {
      const res = await previewMerge({ sourceSectionIds: mergeSourceIds, targetSectionId: mergeTarget });
      setMergePreview(res.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doMergeCommit = async () => {
    try {
      await commitMerge({
        sourceSectionIds: mergeSourceIds,
        targetSectionId: mergeTarget,
        effectiveDate: new Date().toISOString().slice(0, 10),
      });
      setMessage("Merge committed (sources preserved as Merged).");
      await loadSections();
      const hist = await getMergeHistory();
      setMergeHistory(hist.data ?? []);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doSplitPreview = async () => {
    try {
      const res = await previewSplit({ sourceSectionId: splitSource, childCount: 2, strategyCode: "Manual" });
      setSplitPreview(res.data);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doSplitCommit = async () => {
    try {
      await commitSplit({
        sourceSectionId: splitSource,
        strategyCode: "Manual",
        effectiveDate: new Date().toISOString().slice(0, 10),
        children: splitPreview?.proposedChildren,
      });
      setMessage("Split committed (source preserved as Split; children created).");
      await loadSections();
      const hist = await getSplitHistory();
      setSplitHistory(hist.data ?? []);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const doExport = async (kind: string, format: string) => {
    try {
      const res = await exportSectionReport(kind, format);
      const url = URL.createObjectURL(res.data);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${kind}.${format === "excel" ? "xlsx" : format}`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const loadHistoryTab = async () => {
    try {
      const [m, s] = await Promise.all([getMergeHistory(), getSplitHistory()]);
      setMergeHistory(m.data ?? []);
      setSplitHistory(s.data ?? []);
      if (lifecycleSectionId) {
        const hist = await getLifecycleHistory(lifecycleSectionId);
        setLifecycleHistory(hist.data ?? []);
      }
    } catch (e) {
      setError(errMsg(e));
    }
  };

  if (!canView) {
    return (
      <Box sx={{ p: 2 }}>
        <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.sections.view} />
      </Box>
    );
  }

  return (
    <AcademicOperationalPageShell
      title="Sections"
      ariaLabel="Sections management"
      breadcrumb={<AcademicContextBreadcrumb />}
      subtitle="Operational sections under Course → Group → Semester. Subject curriculum is unchanged. Manual attendance remains fully compatible when no section filter is applied."
      headerActions={
        <>
          <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} size="small" sx={academicTouchButtonSx}>
            Academic Setup
          </Button>
          <Button component={RouterLink} to="/setup/academic/allocation-context" size="small" sx={academicTouchButtonSx}>
            Allocation Context
          </Button>
          <Button component={RouterLink} to="/setup/academic/allocation/operations" size="small" sx={academicTouchButtonSx}>
            Allocation Operations
          </Button>
        </>
      }
      error={error}
      onClearError={() => setError(null)}
      message={message}
      onClearMessage={() => setMessage(null)}
      toolbar={
        <AcademicScopeToolbar
          helpTitle="Academic scope"
          helpBody="Select Academic Year, then Course → Group → Semester. Program appears only when Programs are enabled for the tenant."
          actions={
            tab === TAB.List ? (
              <>
                <PermissionAwareButton
                  allowed={canCreate}
                  permissionKey={AcademicPermissionAccess.sections.create}
                  variant="contained"
                  size="small"
                  onClick={openAdd}
                  disabled={!scopeReady}
                  disabledTooltip="Select Academic Year / Course / Group / Semester first."
                  sx={academicTouchButtonSx}
                >
                  Create Section
                </PermissionAwareButton>
                <Button variant="outlined" size="small" onClick={() => void loadSections()} sx={academicTouchButtonSx}>
                  Refresh
                </Button>
              </>
            ) : undefined
          }
        >
          <AcademicScopeSelector
            fields={["academicYear", "program", "course", "group", "semester"]}
            groupOptional
            semesterOptional
            showCascadeHint
            showError={false}
          />
        </AcademicScopeToolbar>
      }
    >
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 1.5 }} variant="scrollable" scrollButtons="auto">
        <Tab label="Section List" />
        <Tab label="Allocation Workspace" />
        <Tab label="Faculty Allocation" />
        <Tab label="Transfer / Auto-Allocate" />
        <Tab label="Lifecycle" />
        <Tab label="Capacity" />
        <Tab label="Merge" />
        <Tab label="Split" />
        <Tab label="Readiness" />
        <Tab label="History" />
      </Tabs>

      {tab === TAB.List && (
        <AcademicDataPanel
          title="Section list"
          accent="academic"
          loading={loading}
          loadingLabel="Loading sections…"
          empty={!loading && rows.length === 0}
          emptyTitle={yearId ? "No sections for this scope" : "Select an Academic Year"}
          emptyDescription={
            yearId
              ? "Create a section or widen Group/Semester filters."
              : "Choose Academic Year (and Course/Group/Semester) to load sections."
          }
          emptyAction={
            <PermissionAwareButton
              allowed={canCreate}
              permissionKey={AcademicPermissionAccess.sections.create}
              variant="contained"
              size="small"
              onClick={openAdd}
              disabled={!scopeReady}
              disabledTooltip="Select Academic Year / Course / Group / Semester first."
            >
              Create Section
            </PermissionAwareButton>
          }
          helpTitle="Section list"
          helpBody="Section is operational student grouping. Capacity and readiness chips use existing section APIs."
        >
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Code</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Status / Lifecycle</TableCell>
                <TableCell>Capacity</TableCell>
                <TableCell>Students</TableCell>
                <TableCell>Available</TableCell>
                <TableCell>Faculty</TableCell>
                <TableCell>Health / Readiness</TableCell>
                <TableCell>Effective Date</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((r) => {
                const stats = statsBySection[r.id];
                const ready = readinessBySection[r.id];
                return (
                  <TableRow key={r.id} hover>
                    <TableCell>{r.sectionCode}</TableCell>
                    <TableCell>{r.sectionName}</TableCell>
                    <TableCell>{r.sectionTypeCode || "Regular"}</TableCell>
                    <TableCell>
                      <AcademicStatusChip label={r.status} status={r.status} variant="outlined" />
                    </TableCell>
                    <TableCell>{r.maximumStrength}</TableCell>
                    <TableCell>{stats?.studentCount ?? r.currentStrength}</TableCell>
                    <TableCell>
                      {stats?.remainingCapacity ?? r.remainingCapacity}
                      {r.capacityStatus ? ` · ${r.capacityStatus}` : ""}
                    </TableCell>
                    <TableCell>{stats?.facultyCount ?? "—"}</TableCell>
                    <TableCell>
                      {ready ? (
                        <AcademicStatusChip
                          label={ready.overallStatus}
                          status={ready.overallStatus}
                          color={readinessColor(ready.overallStatus)}
                        />
                      ) : (
                        "—"
                      )}
                    </TableCell>
                    <TableCell>{effectiveBySection[r.id] || yearStartDate || "—"}</TableCell>
                    <TableCell align="right">
                      <PermissionAwareButton
                        allowed={canEdit}
                        permissionKey={AcademicPermissionAccess.sections.edit}
                        size="small"
                        onClick={() => openEdit(r)}
                        sx={academicTouchButtonSx}
                      >
                        Edit
                      </PermissionAwareButton>
                      <PermissionAwareButton
                        allowed={canDelete}
                        permissionKey={AcademicPermissionAccess.sections.delete}
                        size="small"
                        color="error"
                        onClick={() => setDeleteTargetId(r.id)}
                        sx={academicTouchButtonSx}
                      >
                        Delete
                      </PermissionAwareButton>
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </AcademicDataPanel>
      )}

      {tab === TAB.Students && <EnterpriseAllocationWorkspace />}

      {tab === TAB.Faculty && (
        <FacultySectionAllocationPanel
          academicYearId={yearId}
          courseId={courseId}
          groupId={groupId}
          semesterId={semesterId}
          sections={rows}
          canAssignFaculty={canAssignFaculty}
        />
      )}

      {tab === TAB.Transfer && (
        <Stack spacing={2}>
          {canAssignStudents ? (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Transfer Student
              </Typography>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField size="small" label="Student Id" value={transferStudentId} onChange={(e) => setTransferStudentId(e.target.value)} />
                <TextField
                  select
                  size="small"
                  label="Target Section"
                  value={transferSectionId || ""}
                  onChange={(e) => setTransferSectionId(Number(e.target.value))}
                  sx={{ minWidth: 140 }}
                >
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField size="small" label="Reason" value={transferReason} onChange={(e) => setTransferReason(e.target.value)} sx={{ minWidth: 200 }} />
                <Button variant="contained" onClick={() => void doTransfer()}>
                  Transfer
                </Button>
              </Stack>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Auto Allocation
              </Typography>
              <Stack direction="row" spacing={1}>
                <TextField select size="small" label="Strategy" value={strategy} onChange={(e) => setStrategy(e.target.value)} sx={{ minWidth: 180 }}>
                  <MenuItem value="Alphabetical">Alphabetical</MenuItem>
                  <MenuItem value="GenderBalance">Gender Balance</MenuItem>
                  <MenuItem value="Merit">Merit</MenuItem>
                  <MenuItem value="Random">Random</MenuItem>
                  <MenuItem value="CapacityBased">Capacity Based</MenuItem>
                </TextField>
                <Button variant="outlined" onClick={() => void doAutoAllocate()} disabled={!scopeReady}>
                  Run Auto-Allocate
                </Button>
              </Stack>
            </>
          ) : (
            <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.sections.assignStudents} />
          )}
          <Alert severity="info">
            Combined classes use Section Groups + Timetable → Sections mapping. Attendance history is never rewritten on
            transfer. Readiness never auto-fixes operations. Draft allocation scenarios live under Allocation Context /
            Operations (not live StudentSection writes).
          </Alert>
        </Stack>
      )}

      {tab === TAB.Lifecycle && (
        <Stack spacing={1.5}>
          {!canLifecycleView && !canLifecycleEdit ? (
            <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.sectionLifecycle.view} />
          ) : (
            <>
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Section Lifecycle
              </Typography>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField
                  select
                  size="small"
                  label="Section"
                  value={lifecycleSectionId || ""}
                  onChange={(e) => setLifecycleSectionId(Number(e.target.value))}
                  sx={{ minWidth: 160 }}
                >
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode} ({r.status})
                    </MenuItem>
                  ))}
                </TextField>
                <TextField select size="small" label="Target Status" value={lifecycleTarget} onChange={(e) => setLifecycleTarget(e.target.value)} sx={{ minWidth: 140 }}>
                  {(lifecycleStates.length
                    ? lifecycleStates
                    : ["Draft", "Planning", "Open", "Active", "Locked", "Closed", "Archived"]
                  ).map((s) => (
                    <MenuItem key={s} value={s}>
                      {s}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField size="small" label="Reason" value={lifecycleReason} onChange={(e) => setLifecycleReason(e.target.value)} sx={{ minWidth: 200 }} />
                {canLifecycleEdit && (
                  <Button variant="contained" onClick={() => void doTransition()} disabled={!lifecycleSectionId}>
                    Transition
                  </Button>
                )}
                <Button
                  variant="outlined"
                  onClick={async () => {
                    if (!lifecycleSectionId) return;
                    const hist = await getLifecycleHistory(lifecycleSectionId);
                    setLifecycleHistory(hist.data ?? []);
                  }}
                >
                  Load History
                </Button>
              </Stack>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>From</TableCell>
                    <TableCell>To</TableCell>
                    <TableCell>When</TableCell>
                    <TableCell>Reason</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {lifecycleHistory.map((h, i) => (
                    <TableRow key={i}>
                      <TableCell>{h.fromStatus}</TableCell>
                      <TableCell>{h.toStatus}</TableCell>
                      <TableCell>{h.transitionedUtc}</TableCell>
                      <TableCell>{h.reason}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </>
          )}
        </Stack>
      )}

      {tab === TAB.Capacity && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Capacity
          </Typography>
          {capacitySummary && <Alert severity="info">{capacitySummary}</Alert>}
          <Stack direction="row" spacing={1}>
            <Button variant="outlined" onClick={() => void loadOps()}>
              Refresh Capacity
            </Button>
            <Button variant="outlined" onClick={() => void doExport("section-capacity", "csv")}>
              Export CSV
            </Button>
            <Button variant="outlined" onClick={() => void doExport("section-occupancy", "excel")}>
              Export Excel
            </Button>
          </Stack>
          {canCapacity ? (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Section</TableCell>
                  <TableCell>Lifecycle</TableCell>
                  <TableCell>Occupancy</TableCell>
                  <TableCell>Available</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Warnings</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {occupancy.map((r) => (
                  <TableRow key={r.sectionId}>
                    <TableCell>
                      {r.sectionCode} — {r.sectionName}
                    </TableCell>
                    <TableCell>{r.lifecycleStatus}</TableCell>
                    <TableCell>
                      {r.currentStrength}/{r.maximumCapacity} ({r.occupancyPercent}%)
                    </TableCell>
                    <TableCell>{r.availableSeats}</TableCell>
                    <TableCell>{r.capacityStatus}</TableCell>
                    <TableCell>{r.warnings?.join(" ")}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.sectionCapacity.manage} />
          )}
        </Stack>
      )}

      {tab === TAB.Merge && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Merge
          </Typography>
          {canMerge ? (
            <>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField
                  select
                  size="small"
                  label="Source sections"
                  value={mergeSourceIds}
                  onChange={(e) => {
                    const v = e.target.value;
                    setMergeSourceIds(
                      typeof v === "string" ? v.split(",").map(Number).filter((n) => n > 0) : (v as number[]),
                    );
                  }}
                  slotProps={{
                    select: {
                      multiple: true,
                      renderValue: (selected) =>
                        (selected as number[])
                          .map((id) => rows.find((r) => r.id === id)?.sectionCode ?? id)
                          .join(", "),
                    },
                  }}
                  sx={{ minWidth: 240 }}
                >
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode} — {r.sectionName}
                    </MenuItem>
                  ))}
                </TextField>
                <TextField select size="small" label="Target Section" value={mergeTarget || ""} onChange={(e) => setMergeTarget(Number(e.target.value))} sx={{ minWidth: 160 }}>
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode}
                    </MenuItem>
                  ))}
                </TextField>
                <Button variant="outlined" onClick={() => void doMergePreview()} disabled={!mergeSourceIds.length || !mergeTarget}>
                  Validate / Preview
                </Button>
                <Button variant="contained" onClick={() => void doMergeCommit()} disabled={!mergePreview?.isValid}>
                  Commit Merge
                </Button>
              </Stack>
              {mergePreview && (
                <Alert severity={mergePreview.isValid ? "success" : "error"}>
                  Valid={String(mergePreview.isValid)} · students={mergePreview.combinedStudentCount} · faculty=
                  {mergePreview.combinedFacultyCount}
                  {mergePreview.errors?.length ? ` · ${mergePreview.errors.join(" ")}` : ""}
                  {mergePreview.warnings?.length ? ` · ${mergePreview.warnings.join(" ")}` : ""}
                </Alert>
              )}
            </>
          ) : (
            <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.sectionMergeSplit.merge} />
          )}
        </Stack>
      )}

      {tab === TAB.Split && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Split
          </Typography>
          {canSplit ? (
            <>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <TextField select size="small" label="Source Section" value={splitSource || ""} onChange={(e) => setSplitSource(Number(e.target.value))} sx={{ minWidth: 160 }}>
                  {rows.map((r) => (
                    <MenuItem key={r.id} value={r.id}>
                      {r.sectionCode}
                    </MenuItem>
                  ))}
                </TextField>
                <Button variant="outlined" onClick={() => void doSplitPreview()}>
                  Preview
                </Button>
                <Button variant="contained" onClick={() => void doSplitCommit()} disabled={!splitPreview?.isValid}>
                  Commit Split
                </Button>
              </Stack>
              {splitPreview && (
                <Alert severity={splitPreview.isValid ? "info" : "error"}>
                  Students={splitPreview.sourceStudentCount} · strategy={splitPreview.strategyCode}
                  {splitPreview.proposedChildren?.map((c) => ` · ${c.proposedCode}(${c.plannedStudentCount})`).join("")}
                  {splitPreview.warnings?.length ? ` · ${splitPreview.warnings.join(" ")}` : ""}
                </Alert>
              )}
            </>
          ) : (
            <PermissionDeniedAlert permissionKey={AcademicPermissionAccess.sectionMergeSplit.split} />
          )}
        </Stack>
      )}

      {tab === TAB.Readiness && (
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Operational Readiness (advisory only)
          </Typography>
          <Button variant="outlined" onClick={() => void loadOps()} sx={{ alignSelf: "flex-start" }}>
            Refresh Readiness
          </Button>
          {canReadiness ? (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Section</TableCell>
                  <TableCell>Overall</TableCell>
                  <TableCell>Checks</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {health.map((h) => (
                  <TableRow key={h.sectionId}>
                    <TableCell>
                      {h.sectionCode} — {h.sectionName}
                    </TableCell>
                    <TableCell>
                      <Chip size="small" color={readinessColor(h.overallStatus)} label={h.overallStatus} />
                    </TableCell>
                    <TableCell>{h.checks.map((c) => `${c.area}:${c.status}`).join(" · ")}</TableCell>
                  </TableRow>
                ))}
                {health.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={3}>
                      <Typography variant="body2" color="text.secondary">
                        No readiness rows for this scope.
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          ) : (
            <PermissionDeniedAlert permissionKey={PermissionKeys.SectionReadiness} />
          )}
        </Stack>
      )}

      {tab === TAB.History && (
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            <Button variant="outlined" onClick={() => void loadHistoryTab()}>
              Load History
            </Button>
            <Button variant="outlined" onClick={() => void doExport("merge-history", "csv")}>
              Export Merge CSV
            </Button>
            <Button variant="outlined" onClick={() => void doExport("readiness", "csv")}>
              Export Readiness CSV
            </Button>
            <TextField
              select
              size="small"
              label="Lifecycle section"
              value={lifecycleSectionId || ""}
              onChange={(e) => setLifecycleSectionId(Number(e.target.value))}
              sx={{ minWidth: 160 }}
            >
              {rows.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.sectionCode}
                </MenuItem>
              ))}
            </TextField>
          </Stack>

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Merge History
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Transaction</TableCell>
                <TableCell>Sources → Target</TableCell>
                <TableCell>Effective Date</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Reversed</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {mergeHistory.map((h) => (
                <TableRow key={h.transactionId}>
                  <TableCell>{h.transactionId}</TableCell>
                  <TableCell>
                    {(h.sourceSectionIds ?? []).join("+")} → {h.targetSectionId}
                  </TableCell>
                  <TableCell>{formatDate(h.effectiveDate)}</TableCell>
                  <TableCell>{h.status}</TableCell>
                  <TableCell>{h.isReversed ? "Yes" : "No"}</TableCell>
                </TableRow>
              ))}
              {mergeHistory.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5}>
                    <Typography variant="body2" color="text.secondary">
                      No merge history loaded.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Split History
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Transaction</TableCell>
                <TableCell>Source → Children</TableCell>
                <TableCell>Strategy</TableCell>
                <TableCell>Effective Date</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Reversed</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {splitHistory.map((h) => (
                <TableRow key={h.transactionId}>
                  <TableCell>{h.transactionId}</TableCell>
                  <TableCell>
                    {h.sourceSectionId} → [{(h.childSectionIds ?? []).join(", ")}]
                  </TableCell>
                  <TableCell>{h.strategyCode}</TableCell>
                  <TableCell>{formatDate(h.effectiveDate)}</TableCell>
                  <TableCell>{h.status}</TableCell>
                  <TableCell>{h.isReversed ? "Yes" : "No"}</TableCell>
                </TableRow>
              ))}
              {splitHistory.length === 0 && (
                <TableRow>
                  <TableCell colSpan={6}>
                    <Typography variant="body2" color="text.secondary">
                      No split history loaded.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>

          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Lifecycle Transitions
          </Typography>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>From</TableCell>
                <TableCell>To</TableCell>
                <TableCell>When</TableCell>
                <TableCell>Reason</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {lifecycleHistory.map((h, i) => (
                <TableRow key={i}>
                  <TableCell>{h.fromStatus}</TableCell>
                  <TableCell>{h.toStatus}</TableCell>
                  <TableCell>{h.transitionedUtc}</TableCell>
                  <TableCell>{h.reason}</TableCell>
                </TableRow>
              ))}
              {lifecycleHistory.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4}>
                    <Typography variant="body2" color="text.secondary">
                      Select a section and Load History.
                    </Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </Stack>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit Section" : "Create Section"}</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5} sx={{ mt: 1 }}>
            <TextField label="Section Code" value={code} onChange={(e) => setCode(e.target.value)} helperText="e.g. A, B, C" />
            <TextField label="Section Name" value={name} onChange={(e) => setName(e.target.value)} helperText="e.g. Section A" />
            <TextField
              select
              label="Section Type"
              value={sectionTypeCode}
              onChange={(e) => setSectionTypeCode(e.target.value)}
              helperText="Informational type code from AI29.1B (stored via create/update contracts)."
            >
              <MenuItem value="Regular">Regular</MenuItem>
              <MenuItem value="Honours">Honours</MenuItem>
              <MenuItem value="General">General</MenuItem>
            </TextField>
            <TextField
              type="number"
              label="Maximum Strength"
              value={maxStrength}
              onChange={(e) => setMaxStrength(Number(e.target.value))}
              helperText="College-configurable capacity"
            />
            <TextField type="number" label="Display Order" value={displayOrder} onChange={(e) => setDisplayOrder(Number(e.target.value))} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} sx={academicTouchButtonSx}>
            Cancel
          </Button>
          <Button variant="contained" disabled={saving} onClick={() => void save()} sx={academicTouchButtonSx}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <AcademicConfirmDialog
        open={deleteTargetId != null}
        title="Delete section?"
        description="This soft-deletes the section. Historical student membership is preserved."
        confirmLabel="Delete"
        confirming={deleting}
        onCancel={() => {
          if (!deleting) setDeleteTargetId(null);
        }}
        onConfirm={() => {
          if (deleteTargetId != null) void remove(deleteTargetId);
        }}
      />
    </AcademicOperationalPageShell>
  );
};

export default SectionsPage;
