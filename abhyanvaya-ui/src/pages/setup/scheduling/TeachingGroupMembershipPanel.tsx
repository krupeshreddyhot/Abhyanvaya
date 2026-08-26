import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import AcademicConfirmDialog from "../../../components/academic/AcademicConfirmDialog";
import { getStudents, type StudentRecordDto } from "../../../services/studentsService";
import {
  type ResolvedTeachingGroupMemberDto,
  type TeachingGroupMembershipDto,
} from "../../../services/teachingGroupService";
import {
  addTeachingGroupMembersWithReload,
  removeTeachingGroupMemberWithReload,
  type MembershipAuthoritativeState,
} from "./teachingGroupMembershipActions";
import {
  currentExcludeOverlays,
  currentIncludeOverlays,
  derivedResolvedMembers,
  formatStudentMembershipLabel,
  formatStudentMembershipSecondary,
  isExplicitStudentsSource,
  isHybridSource,
  isMutableMembershipSource,
  isResolvedOverMaxCapacity,
  teachingGroupMemberProvenanceLabel,
  type StudentDisplayHint,
} from "./teachingGroupMembershipUi";
import {
  formatCapacityDisplay,
  teachingGroupMembershipSourceLabel,
} from "./teachingGroupUi";

type ConfirmAction =
  | { kind: "removeInclude"; student: StudentDisplayHint }
  | { kind: "excludeDerived"; student: StudentDisplayHint }
  | { kind: "clearExclude"; student: StudentDisplayHint };

type TeachingGroupMembershipPanelProps = {
  teachingGroupId: number;
  membershipSource: number;
  expectedStudentCount: number | null;
  maxTeachingCapacity: number | null;
  resolvedStudentCount: number;
  memberships: TeachingGroupMembershipDto[];
  resolvedMembers: ResolvedTeachingGroupMemberDto[];
  membershipLoading: boolean;
  canManage: boolean;
  isArchived: boolean;
  courseId?: number;
  groupId?: number;
  onAuthoritativeState: (state: MembershipAuthoritativeState) => void;
  onError: (message: string) => void;
  onMessage: (message: string) => void;
};

const TeachingGroupMembershipPanel = ({
  teachingGroupId,
  membershipSource,
  expectedStudentCount,
  maxTeachingCapacity,
  resolvedStudentCount,
  memberships,
  resolvedMembers,
  membershipLoading,
  canManage,
  isArchived,
  courseId,
  groupId,
  onAuthoritativeState,
  onError,
  onMessage,
}: TeachingGroupMembershipPanelProps) => {
  const mutable = isMutableMembershipSource(membershipSource);
  const explicit = isExplicitStudentsSource(membershipSource);
  const hybrid = isHybridSource(membershipSource);
  const canMutate = canManage && mutable && !isArchived;

  const includes = useMemo(() => currentIncludeOverlays(memberships), [memberships]);
  const excludes = useMemo(() => currentExcludeOverlays(memberships), [memberships]);
  const derived = useMemo(() => derivedResolvedMembers(resolvedMembers), [resolvedMembers]);

  const [studentHints, setStudentHints] = useState<Record<number, StudentDisplayHint>>({});
  const [search, setSearch] = useState("");
  const [searchResults, setSearchResults] = useState<StudentRecordDto[]>([]);
  const [searchLoading, setSearchLoading] = useState(false);
  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [mutating, setMutating] = useState(false);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);

  const onAuthoritativeStateRef = useRef(onAuthoritativeState);
  const onErrorRef = useRef(onError);
  const onMessageRef = useRef(onMessage);
  onAuthoritativeStateRef.current = onAuthoritativeState;
  onErrorRef.current = onError;
  onMessageRef.current = onMessage;

  const overCapacity = isResolvedOverMaxCapacity(resolvedStudentCount, maxTeachingCapacity);

  const rememberStudents = useCallback((students: StudentRecordDto[]) => {
    setStudentHints((prev) => {
      const next = { ...prev };
      for (const s of students) {
        next[s.id] = {
          id: s.id,
          studentNumber: s.studentNumber,
          name: s.name,
          courseName: s.courseName,
          groupName: s.groupName,
        };
      }
      return next;
    });
  }, []);

  const hintFor = useCallback(
    (studentId: number): StudentDisplayHint => studentHints[studentId] ?? { id: studentId },
    [studentHints],
  );

  useEffect(() => {
    setSelectedIds([]);
    setSearch("");
    setSearchResults([]);
    setConfirmAction(null);
  }, [teachingGroupId]);

  useEffect(() => {
    if (!canMutate) {
      setSearchResults([]);
      return;
    }
    const timer = setTimeout(() => {
      void (async () => {
        setSearchLoading(true);
        try {
          const res = await getStudents({
            search: search.trim() || undefined,
            courseId,
            groupId,
            pageNumber: 1,
            pageSize: 20,
          });
          setSearchResults(res.data.data);
          rememberStudents(res.data.data);
        } catch {
          setSearchResults([]);
          onErrorRef.current("Student search failed. Check your connection and try again.");
        } finally {
          setSearchLoading(false);
        }
      })();
    }, 300);
    return () => clearTimeout(timer);
  }, [canMutate, search, courseId, groupId, rememberStudents]);

  const alreadyIncluded = useMemo(
    () => new Set(includes.map((m) => m.studentId)),
    [includes],
  );

  const toggleSelect = (studentId: number) => {
    if (mutating) return;
    setSelectedIds((prev) =>
      prev.includes(studentId) ? prev.filter((id) => id !== studentId) : [...prev, studentId],
    );
  };

  const applyOutcome = (
    outcome: Awaited<ReturnType<typeof addTeachingGroupMembersWithReload>>,
    successMessage?: string,
  ) => {
    if (outcome.kind === "success" || outcome.kind === "conflict") {
      onAuthoritativeStateRef.current(outcome.state);
      setSelectedIds([]);
      if (outcome.kind === "success") onMessageRef.current(successMessage ?? outcome.message);
      else onErrorRef.current(outcome.message);
      return;
    }
    onErrorRef.current(outcome.message);
  };

  const handleAddSelected = async () => {
    if (!canMutate || mutating || selectedIds.length === 0) return;
    setMutating(true);
    onErrorRef.current("");
    try {
      const outcome = await addTeachingGroupMembersWithReload(teachingGroupId, selectedIds);
      applyOutcome(outcome);
    } finally {
      setMutating(false);
    }
  };

  const runConfirmedAction = async () => {
    if (!canMutate || !confirmAction || mutating) return;
    const action = confirmAction;
    setMutating(true);
    onErrorRef.current("");
    try {
      if (action.kind === "clearExclude") {
        const outcome = await addTeachingGroupMembersWithReload(teachingGroupId, [action.student.id]);
        setConfirmAction(null);
        applyOutcome(outcome, "Exclude overlay cleared.");
        return;
      }

      const outcome = await removeTeachingGroupMemberWithReload(teachingGroupId, action.student.id);
      setConfirmAction(null);
      applyOutcome(
        outcome,
        action.kind === "excludeDerived" ? "Student excluded from Hybrid Teaching Group." : undefined,
      );
    } finally {
      setMutating(false);
    }
  };

  const renderStudentRow = (
    studentId: number,
    extra?: string,
    action?: ReactNode,
  ) => {
    const hint = hintFor(studentId);
    const secondary = formatStudentMembershipSecondary(hint);
    return (
      <TableRow key={`${studentId}-${extra ?? "row"}-${action ? "a" : "ro"}`}>
        <TableCell>
          <Typography variant="body2">{formatStudentMembershipLabel(hint)}</Typography>
          {secondary && (
            <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
              {secondary}
            </Typography>
          )}
        </TableCell>
        {extra != null && <TableCell>{extra}</TableCell>}
        {action != null && <TableCell align="right">{action}</TableCell>}
      </TableRow>
    );
  };

  const confirmTitle =
    confirmAction?.kind === "clearExclude"
      ? "Clear exclude overlay?"
      : confirmAction?.kind === "excludeDerived"
        ? "Exclude student from Hybrid group?"
        : "Remove student from membership?";

  const confirmDescription = confirmAction
    ? confirmAction.kind === "clearExclude"
      ? `Clear the exclude overlay for ${formatStudentMembershipLabel(confirmAction.student)} by adding them again through the approved membership API. The server remains authoritative.`
      : confirmAction.kind === "excludeDerived"
        ? `Exclude ${formatStudentMembershipLabel(confirmAction.student)} from this Hybrid Teaching Group? The server will record an exclude overlay.`
        : `Remove ${formatStudentMembershipLabel(confirmAction.student)} from this Teaching Group membership? The server is authoritative.`
    : "";

  const confirmLabel =
    confirmAction?.kind === "clearExclude"
      ? "Clear exclude"
      : confirmAction?.kind === "excludeDerived"
        ? "Exclude"
        : "Remove";

  return (
    <Stack spacing={2} aria-labelledby="tg-membership-heading">
      <Typography id="tg-membership-heading" variant="subtitle1">
        Membership management
      </Typography>

      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        useFlexGap
        sx={{ flexWrap: "wrap" }}
      >
        <Chip size="small" label={`Source: ${teachingGroupMembershipSourceLabel(membershipSource)}`} />
        <Chip size="small" label={`Resolved students: ${resolvedStudentCount}`} />
        <Chip size="small" label={`Expected: ${formatCapacityDisplay(expectedStudentCount)}`} />
        <Chip
          size="small"
          label={`Max teaching capacity: ${formatCapacityDisplay(maxTeachingCapacity)}`}
        />
      </Stack>

      <Typography variant="caption" color="text.secondary">
        Resolved students come from the server membership resolver. Expected is planning intent. Max teaching
        capacity is a teaching ceiling — not room capacity.
      </Typography>

      {overCapacity && (
        <Alert severity="warning" role="status">
          Resolved students ({resolvedStudentCount}) exceed max teaching capacity (
          {maxTeachingCapacity}). Review membership or capacity — the UI will not remove students or create
          groups automatically.
        </Alert>
      )}

      {!mutable && (
        <Alert severity="info" variant="outlined">
          Membership for {teachingGroupMembershipSourceLabel(membershipSource)} Teaching Groups is derived by
          the server. Edit section links or subject enrollment instead of membership overlays. Resolved roster
          below is read-only.
        </Alert>
      )}

      {hybrid && (
        <Alert severity="info" variant="outlined">
          Hybrid membership: include/exclude overlays are editable. Derived and resolved rosters are
          server-calculated and read-only.
        </Alert>
      )}

      {canManage && isArchived && (
        <Alert severity="warning" variant="outlined">
          Archived Teaching Groups cannot change membership.
        </Alert>
      )}

      {!canManage && (
        <Alert severity="info" variant="outlined">
          View-only: Scheduling.TeachingGroup.Manage is required to change membership. The API remains the
          authorization authority.
        </Alert>
      )}

      {membershipLoading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }} role="status" aria-live="polite">
          <CircularProgress size={22} />
          <Typography variant="body2">Loading membership…</Typography>
        </Box>
      ) : (
        <>
          {canMutate && (
            <Box
              sx={{ border: 1, borderColor: "divider", borderRadius: 1, p: 2 }}
              aria-label="Student search and add"
            >
              <Stack spacing={1.5}>
                <TextField
                  label="Search students"
                  size="small"
                  fullWidth
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  disabled={mutating}
                  helperText="Search by name or student number (paginated)."
                  slotProps={{
                    htmlInput: { "aria-label": "Search students by name or number" },
                  }}
                />
                {searchLoading ? (
                  <CircularProgress size={22} aria-label="Searching students" />
                ) : searchResults.length === 0 ? (
                  <Typography variant="body2" color="text.secondary">
                    No students matched this search.
                  </Typography>
                ) : (
                  <List dense disablePadding sx={{ maxHeight: 240, overflow: "auto" }}>
                    {searchResults.map((s) => {
                      const disabled = mutating || alreadyIncluded.has(s.id);
                      const checked = selectedIds.includes(s.id);
                      return (
                        <ListItem key={s.id} disablePadding>
                          <ListItemButton
                            disabled={disabled}
                            onClick={() => toggleSelect(s.id)}
                            aria-label={`Select ${s.studentNumber} ${s.name}`}
                          >
                            <ListItemIcon sx={{ minWidth: 36 }}>
                              <Checkbox
                                edge="start"
                                checked={checked}
                                tabIndex={-1}
                                disableRipple
                                disabled={disabled}
                                slotProps={{
                                  input: { "aria-labelledby": `tg-stu-${s.id}` },
                                }}
                              />
                            </ListItemIcon>
                            <ListItemText
                              id={`tg-stu-${s.id}`}
                              primary={`${s.studentNumber} — ${s.name}`}
                              secondary={`${s.courseName} / ${s.groupName}`}
                            />
                            {alreadyIncluded.has(s.id) && (
                              <Chip size="small" label="Already included" sx={{ ml: 1 }} />
                            )}
                          </ListItemButton>
                        </ListItem>
                      );
                    })}
                  </List>
                )}
                <Box sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}>
                  <Typography variant="body2" color="text.secondary">
                    Selected: {selectedIds.length}
                  </Typography>
                  <Button
                    variant="contained"
                    size="small"
                    disabled={mutating || selectedIds.length === 0}
                    onClick={() => void handleAddSelected()}
                    aria-label="Add selected students to membership"
                  >
                    {mutating ? "Saving…" : "Add selected"}
                  </Button>
                </Box>
              </Stack>
            </Box>
          )}

          {explicit && (
            <>
              <Typography variant="subtitle2">Included students (membership intent)</Typography>
              {includes.length === 0 ? (
                <Alert severity="info" variant="outlined">
                  No explicit members yet. Resolved students: {resolvedStudentCount}.
                </Alert>
              ) : (
                <Table size="small" aria-label="Included students">
                  <TableHead>
                    <TableRow>
                      <TableCell>Student</TableCell>
                      {canMutate && <TableCell align="right">Actions</TableCell>}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {includes.map((m) =>
                      renderStudentRow(
                        m.studentId,
                        undefined,
                        canMutate ? (
                          <Button
                            size="small"
                            color="error"
                            disabled={mutating}
                            onClick={() =>
                              setConfirmAction({ kind: "removeInclude", student: hintFor(m.studentId) })
                            }
                          >
                            Remove
                          </Button>
                        ) : undefined,
                      ),
                    )}
                  </TableBody>
                </Table>
              )}
            </>
          )}

          {hybrid && (
            <Stack spacing={2}>
              <Typography variant="subtitle2">Included students (overlays)</Typography>
              {includes.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  No include overlays.
                </Typography>
              ) : (
                <Table size="small" aria-label="Hybrid included students">
                  <TableHead>
                    <TableRow>
                      <TableCell>Student</TableCell>
                      {canMutate && <TableCell align="right">Actions</TableCell>}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {includes.map((m) =>
                      renderStudentRow(
                        m.studentId,
                        undefined,
                        canMutate ? (
                          <Button
                            size="small"
                            color="error"
                            disabled={mutating}
                            onClick={() =>
                              setConfirmAction({ kind: "removeInclude", student: hintFor(m.studentId) })
                            }
                          >
                            Remove include
                          </Button>
                        ) : undefined,
                      ),
                    )}
                  </TableBody>
                </Table>
              )}

              <Typography variant="subtitle2">Excluded students (overlays)</Typography>
              {excludes.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  No exclude overlays.
                </Typography>
              ) : (
                <Table size="small" aria-label="Hybrid excluded students">
                  <TableHead>
                    <TableRow>
                      <TableCell>Student</TableCell>
                      {canMutate && <TableCell align="right">Actions</TableCell>}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {excludes.map((m) =>
                      renderStudentRow(
                        m.studentId,
                        undefined,
                        canMutate ? (
                          <Button
                            size="small"
                            disabled={mutating}
                            onClick={() =>
                              setConfirmAction({ kind: "clearExclude", student: hintFor(m.studentId) })
                            }
                          >
                            Clear exclude
                          </Button>
                        ) : undefined,
                      ),
                    )}
                  </TableBody>
                </Table>
              )}

              <Typography variant="subtitle2">Derived students (read-only roster; exclude via API)</Typography>
              {derived.length === 0 ? (
                <Typography variant="body2" color="text.secondary">
                  No derived students in the resolved roster.
                </Typography>
              ) : (
                <Table size="small" aria-label="Derived students">
                  <TableHead>
                    <TableRow>
                      <TableCell>Student</TableCell>
                      <TableCell>Provenance</TableCell>
                      {canMutate && <TableCell align="right">Actions</TableCell>}
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {derived.map((r) =>
                      renderStudentRow(
                        r.studentId,
                        teachingGroupMemberProvenanceLabel(r.provenance),
                        canMutate ? (
                          <Button
                            size="small"
                            color="warning"
                            disabled={mutating}
                            onClick={() =>
                              setConfirmAction({ kind: "excludeDerived", student: hintFor(r.studentId) })
                            }
                          >
                            Exclude
                          </Button>
                        ) : undefined,
                      ),
                    )}
                  </TableBody>
                </Table>
              )}
            </Stack>
          )}

          <Typography variant="subtitle2">Resolved students (server, read-only)</Typography>
          {resolvedMembers.length === 0 ? (
            <Alert severity="info" variant="outlined">
              No resolved members. An empty Teaching Group is allowed when the domain permits it.
            </Alert>
          ) : (
            <Table size="small" aria-label="Resolved students read-only">
              <TableHead>
                <TableRow>
                  <TableCell>Student</TableCell>
                  <TableCell>Provenance</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {resolvedMembers.map((r) =>
                  renderStudentRow(r.studentId, teachingGroupMemberProvenanceLabel(r.provenance)),
                )}
              </TableBody>
            </Table>
          )}

          {!explicit && !hybrid && memberships.length > 0 && (
            <>
              <Typography variant="subtitle2">Membership overlays (read-only)</Typography>
              <Table size="small" aria-label="Membership overlays read-only">
                <TableHead>
                  <TableRow>
                    <TableCell>Student</TableCell>
                    <TableCell>Inclusion</TableCell>
                    <TableCell>Current</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {memberships.map((m) => (
                    <TableRow key={m.id}>
                      <TableCell>{formatStudentMembershipLabel(hintFor(m.studentId))}</TableCell>
                      <TableCell>{m.inclusion === 1 ? "Include" : "Exclude"}</TableCell>
                      <TableCell>{m.isCurrent ? "Yes" : "No"}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </>
          )}
        </>
      )}

      <AcademicConfirmDialog
        open={confirmAction != null}
        title={confirmTitle}
        description={confirmDescription}
        confirmLabel={confirmLabel}
        confirmColor="error"
        confirming={mutating}
        onCancel={() => !mutating && setConfirmAction(null)}
        onConfirm={() => void runConfirmedAction()}
      />
    </Stack>
  );
};

export default TeachingGroupMembershipPanel;
