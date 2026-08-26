import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  FormControl,
  InputLabel,
  MenuItem,
  Select,
  Stack,
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
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import AcademicConfirmDialog from "../../../components/academic/AcademicConfirmDialog";
import { listSubjectCatalog } from "../../../services/setupService";
import { listSubjectAllocations, type SubjectAllocationDto } from "../../../services/schedulingService";
import { listSections, type SectionDto } from "../../../services/sectionService";
import {
  addTeachingGroupSection,
  archiveTeachingGroup,
  createTeachingGroup,
  getTeachingGroup,
  getTeachingGroupMemberships,
  getResolvedTeachingGroupMembers,
  removeTeachingGroupSection,
  replaceTeachingGroupSections,
  listTeachingGroups,
  updateTeachingGroup,
  TeachingGroupActivityKind,
  TeachingGroupMembershipSource,
  TeachingGroupStatus,
  TeachingGroupType,
  type CreateTeachingGroupRequest,
  type ResolvedTeachingGroupMemberDto,
  type TeachingGroupDetailDto,
  type TeachingGroupMembershipDto,
  type TeachingGroupSummaryDto,
  type UpdateTeachingGroupRequest,
} from "../../../services/teachingGroupService";
import { getApiErrorMessage } from "../../../utils/apiErrorMessage";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";
import TeachingGroupMembershipPanel from "./TeachingGroupMembershipPanel";
import type { MembershipAuthoritativeState } from "./teachingGroupMembershipActions";
import {
  formatCapacityDisplay,
  parseOptionalCapacity,
  teachingGroupActivityKindLabel,
  teachingGroupMembershipSourceLabel,
  teachingGroupStatusLabel,
  teachingGroupTypeLabel,
} from "./teachingGroupUi";

type CreateFormState = {
  name: string;
  code: string;
  type: TeachingGroupType;
  membershipSource: TeachingGroupMembershipSource;
  activityKind: TeachingGroupActivityKind;
  expectedStudentCount: string;
  maxTeachingCapacity: string;
  exclusionGroupKey: string;
  notes: string;
};

const emptyCreateForm = (): CreateFormState => ({
  name: "",
  code: "",
  type: TeachingGroupType.Custom,
  membershipSource: TeachingGroupMembershipSource.ExplicitStudents,
  activityKind: TeachingGroupActivityKind.Lecture,
  expectedStudentCount: "",
  maxTeachingCapacity: "",
  exclusionGroupKey: "",
  notes: "",
});

const TeachingGroupsPage = () => {
  const { hasPermission, hasAnyPermission } = useAuth();
  // Match AppRoutes + API CanViewSchedulingTeachingGroup (TG keys or Scheduling.View/Manage).
  const canView = hasAnyPermission([
    PermissionKeys.SchedulingTeachingGroupView,
    PermissionKeys.SchedulingTeachingGroupManage,
    PermissionKeys.SchedulingView,
    PermissionKeys.SchedulingManage,
  ]);
  const canManage = hasPermission(PermissionKeys.SchedulingTeachingGroupManage);

  const [allocations, setAllocations] = useState<SubjectAllocationDto[]>([]);
  const [subjectNames, setSubjectNames] = useState<Record<number, string>>({});
  const [subjectAllocationId, setSubjectAllocationId] = useState<number | "">("");

  const [rows, setRows] = useState<TeachingGroupSummaryDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [createForm, setCreateForm] = useState<CreateFormState>(emptyCreateForm);
  const [saving, setSaving] = useState(false);

  const [detail, setDetail] = useState<TeachingGroupDetailDto | null>(null);
  const [memberships, setMemberships] = useState<TeachingGroupMembershipDto[]>([]);
  const [resolvedMembers, setResolvedMembers] = useState<ResolvedTeachingGroupMemberDto[]>([]);
  const [membershipLoading, setMembershipLoading] = useState(false);
  const [detailLoading, setDetailLoading] = useState(false);
  const [editForm, setEditForm] = useState<UpdateTeachingGroupRequest | null>(null);

  const [availableSections, setAvailableSections] = useState<SectionDto[]>([]);
  const [sectionToAdd, setSectionToAdd] = useState<number | "">("");
  const [sectionBusy, setSectionBusy] = useState(false);

  const [archiveTarget, setArchiveTarget] = useState<TeachingGroupSummaryDto | TeachingGroupDetailDto | null>(
    null,
  );
  const [archiving, setArchiving] = useState(false);

  const selectedAllocation = useMemo(
    () => (subjectAllocationId === "" ? null : allocations.find((a) => a.id === subjectAllocationId) ?? null),
    [allocations, subjectAllocationId],
  );

  const allocationLabel = useCallback(
    (a: SubjectAllocationDto) => {
      const subject = subjectNames[a.subjectId] ?? `Subject ${a.subjectId}`;
      return `#${a.id} — ${subject} (Course ${a.courseId} · Group ${a.groupId} · Sem ${a.semesterId})`;
    },
    [subjectNames],
  );

  useEffect(() => {
    void (async () => {
      setCatalogLoading(true);
      setError(null);
      try {
        const [allocRes, subjectRes] = await Promise.all([
          listSubjectAllocations(),
          listSubjectCatalog().catch(() => ({ data: [] as { id: number; name: string }[] })),
        ]);
        setAllocations(allocRes.data);
        const map: Record<number, string> = {};
        for (const s of subjectRes.data) {
          map[s.id] = s.name || `Subject ${s.id}`;
        }
        setSubjectNames(map);
      } catch (e) {
        setError(errMsg(e));
      } finally {
        setCatalogLoading(false);
      }
    })();
  }, []);

  const loadList = useCallback(async (allocationId: number) => {
    setLoading(true);
    setError(null);
    try {
      const res = await listTeachingGroups(allocationId);
      setRows(res.data);
    } catch (e) {
      setRows([]);
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    setDetail(null);
    setMemberships([]);
    setResolvedMembers([]);
    setEditForm(null);
    setRows([]);
    if (subjectAllocationId === "") return;
    void loadList(subjectAllocationId);
  }, [subjectAllocationId, loadList]);

  const applyMembershipState = useCallback(
    (state: MembershipAuthoritativeState) => {
      setDetail(state.detail);
      setMemberships(state.memberships);
      setResolvedMembers(state.resolvedMembers);
      setEditForm((prev) =>
        prev
          ? {
              ...prev,
              name: state.detail.name,
              code: state.detail.code,
              activityKind: state.detail.activityKind,
              expectedStudentCount: state.detail.expectedStudentCount,
              maxTeachingCapacity: state.detail.maxTeachingCapacity,
              exclusionGroupKey: state.detail.exclusionGroupKey,
              effectiveFrom: state.detail.effectiveFrom,
              effectiveTo: state.detail.effectiveTo,
              notes: state.detail.notes,
              displayOrder: state.detail.displayOrder,
            }
          : {
              name: state.detail.name,
              code: state.detail.code,
              activityKind: state.detail.activityKind,
              expectedStudentCount: state.detail.expectedStudentCount,
              maxTeachingCapacity: state.detail.maxTeachingCapacity,
              exclusionGroupKey: state.detail.exclusionGroupKey,
              effectiveFrom: state.detail.effectiveFrom,
              effectiveTo: state.detail.effectiveTo,
              notes: state.detail.notes,
              displayOrder: state.detail.displayOrder,
            },
      );
    },
    [],
  );

  const openDetail = async (id: number) => {
    setDetailLoading(true);
    setMembershipLoading(true);
    setError(null);
    setMessage(null);
    try {
      const [detailRes, membershipRes, resolvedRes] = await Promise.all([
        getTeachingGroup(id),
        getTeachingGroupMemberships(id),
        getResolvedTeachingGroupMembers(id),
      ]);
      setDetail(detailRes.data);
      setMemberships(membershipRes.data);
      setResolvedMembers(resolvedRes.data);
      setEditForm({
        name: detailRes.data.name,
        code: detailRes.data.code,
        activityKind: detailRes.data.activityKind,
        expectedStudentCount: detailRes.data.expectedStudentCount,
        maxTeachingCapacity: detailRes.data.maxTeachingCapacity,
        exclusionGroupKey: detailRes.data.exclusionGroupKey,
        effectiveFrom: detailRes.data.effectiveFrom,
        effectiveTo: detailRes.data.effectiveTo,
        notes: detailRes.data.notes,
        displayOrder: detailRes.data.displayOrder,
      });

      const sa = allocations.find((a) => a.id === detailRes.data.subjectAllocationId);
      if (sa) {
        const sectionsRes = await listSections({
          academicYearId: sa.academicYearId,
          courseId: sa.courseId,
          groupId: sa.groupId,
          semesterId: sa.semesterId,
        });
        setAvailableSections(sectionsRes.data);
      } else {
        setAvailableSections([]);
      }
      setSectionToAdd("");
    } catch (e) {
      setDetail(null);
      setMemberships([]);
      setResolvedMembers([]);
      setError(getApiErrorMessage(e, errMsg(e)));
    } finally {
      setDetailLoading(false);
      setMembershipLoading(false);
    }
  };

  const openCreate = () => {
    if (!canManage || subjectAllocationId === "") return;
    setCreateForm(emptyCreateForm());
    setCreateOpen(true);
  };

  const submitCreate = async () => {
    if (!canManage || subjectAllocationId === "") return;
    if (!createForm.name.trim()) {
      setError("Teaching Group name is required.");
      return;
    }
    const expected = parseOptionalCapacity(createForm.expectedStudentCount);
    const max = parseOptionalCapacity(createForm.maxTeachingCapacity);
    if (Number.isNaN(expected) || Number.isNaN(max)) {
      setError("Capacity values must be whole numbers when provided.");
      return;
    }

    const payload: CreateTeachingGroupRequest = {
      subjectAllocationId,
      name: createForm.name.trim(),
      code: createForm.code.trim() || null,
      type: createForm.type,
      membershipSource: createForm.membershipSource,
      activityKind: createForm.activityKind,
      expectedStudentCount: expected,
      maxTeachingCapacity: max,
      exclusionGroupKey: createForm.exclusionGroupKey.trim() || null,
      notes: createForm.notes.trim() || null,
      displayOrder: 0,
    };

    setSaving(true);
    setError(null);
    try {
      const res = await createTeachingGroup(payload);
      setCreateOpen(false);
      setMessage(`Teaching Group “${res.data.name}” created.`);
      await loadList(subjectAllocationId);
      await openDetail(res.data.id);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const submitUpdate = async () => {
    if (!canManage || !detail || !editForm) return;
    if (detail.status === TeachingGroupStatus.Archived) {
      setError("Archived Teaching Groups cannot be edited.");
      return;
    }
    if (!editForm.name.trim()) {
      setError("Teaching Group name is required.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const res = await updateTeachingGroup(detail.id, {
        ...editForm,
        name: editForm.name.trim(),
        code: editForm.code?.trim() || null,
        notes: editForm.notes?.trim() || null,
        exclusionGroupKey: editForm.exclusionGroupKey?.trim() || null,
      });
      setDetail(res.data);
      setMessage("Teaching Group updated.");
      if (subjectAllocationId !== "") await loadList(subjectAllocationId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const confirmArchive = async () => {
    if (!canManage || !archiveTarget) return;
    setArchiving(true);
    setError(null);
    try {
      const res = await archiveTeachingGroup(archiveTarget.id);
      setMessage(`Teaching Group “${res.data.name}” archived.`);
      setArchiveTarget(null);
      if (detail?.id === res.data.id) {
        setDetail(res.data);
        setEditForm((prev) => (prev ? { ...prev } : prev));
      }
      if (subjectAllocationId !== "") await loadList(subjectAllocationId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setArchiving(false);
    }
  };

  const handleAddSection = async () => {
    if (!canManage || !detail || sectionToAdd === "") return;
    setSectionBusy(true);
    setError(null);
    try {
      await addTeachingGroupSection(detail.id, sectionToAdd);
      setMessage("Sections updated successfully.");
      await openDetail(detail.id);
      if (subjectAllocationId !== "") await loadList(subjectAllocationId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSectionBusy(false);
    }
  };

  const handleRemoveSection = async (sectionId: number) => {
    if (!canManage || !detail) return;
    setSectionBusy(true);
    setError(null);
    try {
      await removeTeachingGroupSection(detail.id, sectionId);
      setMessage("Sections updated successfully.");
      await openDetail(detail.id);
      if (subjectAllocationId !== "") await loadList(subjectAllocationId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSectionBusy(false);
    }
  };

  const handleReplaceSections = async (sectionIds: number[]) => {
    if (!canManage || !detail) return;
    setSectionBusy(true);
    setError(null);
    try {
      await replaceTeachingGroupSections(detail.id, { sectionIds });
      setMessage("Sections updated successfully.");
      await openDetail(detail.id);
      if (subjectAllocationId !== "") await loadList(subjectAllocationId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSectionBusy(false);
    }
  };

  const linkedSectionIds = new Set(detail?.sections.map((s) => s.sectionId) ?? []);
  const sectionsAvailableToAdd = availableSections.filter((s) => !linkedSectionIds.has(s.id));
  const isArchived = detail?.status === TeachingGroupStatus.Archived;

  if (!canView && !canManage) {
    return (
      <Stack spacing={2}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Alert severity="warning">
          Scheduling.TeachingGroup.View (or Scheduling.View / Scheduling.Manage) is required to open Teaching
          Groups. The API remains the authorization authority.
        </Alert>
      </Stack>
    );
  }

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Teaching Groups
        </Typography>
        {canManage && (
          <Button
            variant="contained"
            disabled={subjectAllocationId === "" || catalogLoading}
            onClick={openCreate}
          >
            Create Teaching Group
          </Button>
        )}
      </Box>

      <Alert severity="info" variant="outlined">
        One Subject Allocation may own many Teaching Groups. Groups are never created automatically — create
        them explicitly. Section membership uses Teaching Group sections (source of truth); timetable section
        links are updated by the server.
      </Alert>

      {message && (
        <Alert severity="success" onClose={() => setMessage(null)}>
          {message}
        </Alert>
      )}
      {error && (
        <Alert severity="error" onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <FormControl size="small" sx={{ minWidth: 360, maxWidth: 720 }}>
        <InputLabel id="tg-sa">Subject Allocation</InputLabel>
        <Select
          labelId="tg-sa"
          label="Subject Allocation"
          value={subjectAllocationId === "" ? "" : subjectAllocationId}
          onChange={(e) => setSubjectAllocationId(parseOptionalSelectNumber(e.target.value))}
          disabled={catalogLoading}
        >
          <MenuItem value="">
            <em>Select a Subject Allocation</em>
          </MenuItem>
          {allocations.map((a) => (
            <MenuItem key={a.id} value={a.id}>
              {allocationLabel(a)}
            </MenuItem>
          ))}
        </Select>
      </FormControl>

      {selectedAllocation && (
        <Typography variant="body2" color="text.secondary">
          Context: {allocationLabel(selectedAllocation)} — listing Teaching Groups for this allocation only.
        </Typography>
      )}

      {catalogLoading ? (
        <CircularProgress />
      ) : subjectAllocationId === "" ? (
        <Alert severity="info">Select a Subject Allocation to view its Teaching Groups.</Alert>
      ) : loading ? (
        <CircularProgress />
      ) : rows.length === 0 ? (
        <Alert severity="info">
          No Teaching Groups have been created for this Subject Allocation.
          {canManage ? " Use Create Teaching Group to add one." : ""}
        </Alert>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Membership source</TableCell>
              <TableCell align="right">Expected</TableCell>
              <TableCell align="right">Max capacity</TableCell>
              <TableCell align="right">Resolved</TableCell>
              <TableCell align="right">Sections</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover selected={detail?.id === r.id}>
                <TableCell>{r.code ?? "—"}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{teachingGroupTypeLabel(r.type)}</TableCell>
                <TableCell>
                  <Chip size="small" label={teachingGroupStatusLabel(r.status)} />
                </TableCell>
                <TableCell>{teachingGroupMembershipSourceLabel(r.membershipSource)}</TableCell>
                <TableCell align="right">{formatCapacityDisplay(r.expectedStudentCount)}</TableCell>
                <TableCell align="right">{formatCapacityDisplay(r.maxTeachingCapacity)}</TableCell>
                <TableCell align="right">{r.resolvedStudentCount}</TableCell>
                <TableCell align="right">{r.linkedSectionCount}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => void openDetail(r.id)}>
                    Open
                  </Button>
                  {canManage && r.status !== TeachingGroupStatus.Archived && (
                    <Button size="small" color="warning" onClick={() => setArchiveTarget(r)}>
                      Archive
                    </Button>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      {detailLoading && <CircularProgress size={28} />}

      {detail && editForm && (
        <Box sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 2 }}>
          <Stack spacing={2}>
            <Typography variant="h6">
              {detail.name}{" "}
              <Chip size="small" label={teachingGroupStatusLabel(detail.status)} sx={{ ml: 1 }} />
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Type: {teachingGroupTypeLabel(detail.type)} · Activity:{" "}
              {teachingGroupActivityKindLabel(detail.activityKind)} · SA #{detail.subjectAllocationId} ·
              Resolved students: {detail.resolvedStudentCount} (derived)
            </Typography>

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField
                label="Name"
                size="small"
                fullWidth
                value={editForm.name}
                disabled={!canManage || isArchived}
                onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
              />
              <TextField
                label="Code"
                size="small"
                fullWidth
                value={editForm.code ?? ""}
                disabled={!canManage || isArchived}
                onChange={(e) => setEditForm({ ...editForm, code: e.target.value })}
              />
              <FormControl size="small" fullWidth disabled={!canManage || isArchived}>
                <InputLabel id="tg-act">Activity</InputLabel>
                <Select
                  labelId="tg-act"
                  label="Activity"
                  value={editForm.activityKind}
                  onChange={(e) =>
                    setEditForm({ ...editForm, activityKind: Number(e.target.value) as TeachingGroupActivityKind })
                  }
                >
                  {Object.entries(TeachingGroupActivityKind).map(([k, v]) => (
                    <MenuItem key={k} value={v}>
                      {teachingGroupActivityKindLabel(v)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Stack>

            <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
              <TextField
                label="Expected student count"
                size="small"
                fullWidth
                value={editForm.expectedStudentCount ?? ""}
                disabled={!canManage || isArchived}
                onChange={(e) => {
                  const v = parseOptionalCapacity(e.target.value);
                  setEditForm({
                    ...editForm,
                    expectedStudentCount: e.target.value.trim() === "" ? null : Number.isNaN(v) ? editForm.expectedStudentCount : v,
                  });
                }}
                helperText="Planning intent (optional)"
              />
              <TextField
                label="Max teaching capacity"
                size="small"
                fullWidth
                value={editForm.maxTeachingCapacity ?? ""}
                disabled={!canManage || isArchived}
                onChange={(e) => {
                  const v = parseOptionalCapacity(e.target.value);
                  setEditForm({
                    ...editForm,
                    maxTeachingCapacity: e.target.value.trim() === "" ? null : Number.isNaN(v) ? editForm.maxTeachingCapacity : v,
                  });
                }}
                helperText="Optional ceiling (not room capacity)"
              />
              <TextField
                label="Resolved student count"
                size="small"
                fullWidth
                value={detail.resolvedStudentCount}
                disabled
                helperText="Derived from membership — not editable"
              />
            </Stack>

            <TextField
              label="Notes"
              size="small"
              fullWidth
              multiline
              minRows={2}
              value={editForm.notes ?? ""}
              disabled={!canManage || isArchived}
              onChange={(e) => setEditForm({ ...editForm, notes: e.target.value })}
            />

            {canManage && !isArchived && (
              <Box sx={{ display: "flex", gap: 1, flexWrap: "wrap" }}>
                <Button variant="contained" disabled={saving} onClick={() => void submitUpdate()}>
                  Save changes
                </Button>
                <Button color="warning" disabled={saving} onClick={() => setArchiveTarget(detail)}>
                  Archive
                </Button>
              </Box>
            )}

            <Divider />

            <Typography variant="subtitle1">Sections</Typography>
            <Typography variant="body2" color="text.secondary">
              Managed via Teaching Group section links (source of truth). Timetable projections update on the
              server after changes.
            </Typography>
            {detail.sections.length === 0 ? (
              <Alert severity="info" variant="outlined">
                No sections are linked to this Teaching Group yet.
              </Alert>
            ) : (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Section</TableCell>
                    <TableCell>Primary</TableCell>
                    {canManage && !isArchived && <TableCell align="right">Actions</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {detail.sections.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell>
                        {s.sectionCode ?? s.sectionId} {s.sectionName ? `— ${s.sectionName}` : ""}
                      </TableCell>
                      <TableCell>{s.isPrimary ? "Yes" : "No"}</TableCell>
                      {canManage && !isArchived && (
                        <TableCell align="right">
                          <Button
                            size="small"
                            color="error"
                            disabled={sectionBusy}
                            onClick={() => void handleRemoveSection(s.sectionId)}
                          >
                            Remove
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}

            {canManage && !isArchived && (
              <Stack direction={{ xs: "column", sm: "row" }} spacing={1} sx={{ alignItems: "center" }}>
                <FormControl size="small" sx={{ minWidth: 240, flexGrow: 1 }}>
                  <InputLabel id="tg-add-sec">Add section</InputLabel>
                  <Select
                    labelId="tg-add-sec"
                    label="Add section"
                    value={sectionToAdd === "" ? "" : sectionToAdd}
                    onChange={(e) => setSectionToAdd(parseOptionalSelectNumber(e.target.value))}
                  >
                    <MenuItem value="">
                      <em>Select section</em>
                    </MenuItem>
                    {sectionsAvailableToAdd.map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        {s.sectionCode} — {s.sectionName}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <Button
                  variant="outlined"
                  disabled={sectionBusy || sectionToAdd === ""}
                  onClick={() => void handleAddSection()}
                >
                  Add section
                </Button>
                {detail.sections.length > 0 && (
                  <Button
                    variant="text"
                    color="warning"
                    disabled={sectionBusy}
                    onClick={() => void handleReplaceSections([])}
                  >
                    Clear all sections
                  </Button>
                )}
              </Stack>
            )}

            <Divider />

            <TeachingGroupMembershipPanel
              teachingGroupId={detail.id}
              membershipSource={detail.membershipSource}
              expectedStudentCount={detail.expectedStudentCount}
              maxTeachingCapacity={detail.maxTeachingCapacity}
              resolvedStudentCount={detail.resolvedStudentCount}
              memberships={memberships}
              resolvedMembers={resolvedMembers}
              membershipLoading={membershipLoading}
              canManage={canManage}
              isArchived={!!isArchived}
              courseId={selectedAllocation?.courseId ?? detail.courseId}
              groupId={selectedAllocation?.groupId ?? detail.groupId}
              onAuthoritativeState={(state) => {
                applyMembershipState(state);
                if (subjectAllocationId !== "") void loadList(subjectAllocationId);
              }}
              onError={(msg) => {
                setMessage(null);
                setError(msg || null);
              }}
              onMessage={(msg) => {
                setError(null);
                setMessage(msg);
              }}
            />
          </Stack>
        </Box>
      )}

      <Dialog open={createOpen} onClose={() => !saving && setCreateOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Create Teaching Group</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Subject Allocation #{subjectAllocationId} — academic scope is taken from the allocation on the
              server.
            </Typography>
            <TextField
              label="Name"
              required
              size="small"
              fullWidth
              value={createForm.name}
              onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
            />
            <TextField
              label="Code"
              size="small"
              fullWidth
              value={createForm.code}
              onChange={(e) => setCreateForm({ ...createForm, code: e.target.value })}
            />
            <FormControl size="small" fullWidth>
              <InputLabel id="tg-type">Type</InputLabel>
              <Select
                labelId="tg-type"
                label="Type"
                value={createForm.type}
                onChange={(e) =>
                  setCreateForm({ ...createForm, type: Number(e.target.value) as TeachingGroupType })
                }
              >
                {Object.entries(TeachingGroupType).map(([k, v]) => (
                  <MenuItem key={k} value={v}>
                    {teachingGroupTypeLabel(v)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" fullWidth>
              <InputLabel id="tg-ms">Membership source</InputLabel>
              <Select
                labelId="tg-ms"
                label="Membership source"
                value={createForm.membershipSource}
                onChange={(e) =>
                  setCreateForm({
                    ...createForm,
                    membershipSource: Number(e.target.value) as TeachingGroupMembershipSource,
                  })
                }
              >
                {Object.entries(TeachingGroupMembershipSource).map(([k, v]) => (
                  <MenuItem key={k} value={v}>
                    {teachingGroupMembershipSourceLabel(v)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <FormControl size="small" fullWidth>
              <InputLabel id="tg-create-act">Activity</InputLabel>
              <Select
                labelId="tg-create-act"
                label="Activity"
                value={createForm.activityKind}
                onChange={(e) =>
                  setCreateForm({
                    ...createForm,
                    activityKind: Number(e.target.value) as TeachingGroupActivityKind,
                  })
                }
              >
                {Object.entries(TeachingGroupActivityKind).map(([k, v]) => (
                  <MenuItem key={k} value={v}>
                    {teachingGroupActivityKindLabel(v)}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
            <TextField
              label="Expected student count"
              size="small"
              fullWidth
              value={createForm.expectedStudentCount}
              onChange={(e) => setCreateForm({ ...createForm, expectedStudentCount: e.target.value })}
            />
            <TextField
              label="Max teaching capacity"
              size="small"
              fullWidth
              value={createForm.maxTeachingCapacity}
              onChange={(e) => setCreateForm({ ...createForm, maxTeachingCapacity: e.target.value })}
            />
            {createForm.type === TeachingGroupType.CapacitySplit && (
              <TextField
                label="Exclusion group key"
                required
                size="small"
                fullWidth
                value={createForm.exclusionGroupKey}
                onChange={(e) => setCreateForm({ ...createForm, exclusionGroupKey: e.target.value })}
                helperText="Required for Capacity split groups"
              />
            )}
            <TextField
              label="Notes"
              size="small"
              fullWidth
              multiline
              minRows={2}
              value={createForm.notes}
              onChange={(e) => setCreateForm({ ...createForm, notes: e.target.value })}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button disabled={saving} onClick={() => setCreateOpen(false)}>
            Cancel
          </Button>
          <Button variant="contained" disabled={saving} onClick={() => void submitCreate()}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      <AcademicConfirmDialog
        open={archiveTarget != null}
        title="Archive Teaching Group?"
        description="Archived Teaching Groups cannot be used for new scheduling mutations where the server enforces lifecycle rules. This does not hard-delete the group."
        confirmLabel="Archive"
        confirmColor="warning"
        confirming={archiving}
        onCancel={() => setArchiveTarget(null)}
        onConfirm={() => void confirmArchive()}
      />
    </Stack>
  );
};

export default TeachingGroupsPage;
