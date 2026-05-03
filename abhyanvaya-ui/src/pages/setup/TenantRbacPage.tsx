import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  FormGroup,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { useAuth } from "../../context/AuthContext";
import {
  createApplicationRole,
  deleteApplicationRole,
  getApplicationRole,
  listApplicationRoles,
  listPermissionCatalog,
  listTenantUsersRbac,
  setRolePermissions,
  setUserApplicationRoles,
  updateApplicationRole,
  type ApplicationRoleListItem,
  type PermissionCatalogItem,
  type TenantUserRbacRow,
} from "../../services/rbacService";
import {
  adminResetUserPassword,
  createTenantUser,
  linkUserStaff,
} from "../../services/tenantUsersService";
import {
  listMasterCourses,
  listMasterGroups,
  listStaff,
  type CourseRow,
  type GroupRow,
  type StaffListItem,
} from "../../services/setupService";

const errMsg = (e: unknown): string => {
  const d = (e as { response?: { data?: unknown } }).response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const groupByResource = (items: PermissionCatalogItem[]) => {
  const map = new Map<string, PermissionCatalogItem[]>();
  for (const p of items) {
    const list = map.get(p.resource) ?? [];
    list.push(p);
    map.set(p.resource, list);
  }
  return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]));
};

const TenantRbacPage = () => {
  const { user: authUser } = useAuth();
  const myUserId = authUser?.userId ?? 0;

  const [tab, setTab] = useState(0);
  const [permCatalog, setPermCatalog] = useState<PermissionCatalogItem[]>([]);
  const [roles, setRoles] = useState<ApplicationRoleListItem[]>([]);
  const [users, setUsers] = useState<TenantUserRbacRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [createName, setCreateName] = useState("");
  const [createCode, setCreateCode] = useState("");
  const [createDesc, setCreateDesc] = useState("");
  const [savingCreate, setSavingCreate] = useState(false);

  const [editMetaOpen, setEditMetaOpen] = useState(false);
  const [editMetaId, setEditMetaId] = useState(0);
  const [editMetaName, setEditMetaName] = useState("");
  const [editMetaDesc, setEditMetaDesc] = useState("");
  const [savingMeta, setSavingMeta] = useState(false);

  const [permOpen, setPermOpen] = useState(false);
  const [permRoleId, setPermRoleId] = useState(0);
  const [permRoleLabel, setPermRoleLabel] = useState("");
  const [selectedPermIds, setSelectedPermIds] = useState<Set<number>>(new Set());
  const [savingPerm, setSavingPerm] = useState(false);

  const [userEditOpen, setUserEditOpen] = useState(false);
  const [userEditRow, setUserEditRow] = useState<TenantUserRbacRow | null>(null);
  const [userRolePick, setUserRolePick] = useState<ApplicationRoleListItem[]>([]);
  const [savingUser, setSavingUser] = useState(false);

  const [createUserOpen, setCreateUserOpen] = useState(false);
  const [cuUsername, setCuUsername] = useState("");
  const [cuPassword, setCuPassword] = useState("");
  const [cuLoginRole, setCuLoginRole] = useState<"Admin" | "Faculty">("Faculty");
  const [cuCourseId, setCuCourseId] = useState(0);
  const [cuGroupId, setCuGroupId] = useState(0);
  const [cuStaff, setCuStaff] = useState<StaffListItem | null>(null);
  const [staffItems, setStaffItems] = useState<StaffListItem[]>([]);
  const [cuAppRoles, setCuAppRoles] = useState<ApplicationRoleListItem[]>([]);
  const [courses, setCourses] = useState<CourseRow[]>([]);
  const [groups, setGroups] = useState<GroupRow[]>([]);
  const [savingCu, setSavingCu] = useState(false);

  const [staffItemsEdit, setStaffItemsEdit] = useState<StaffListItem[]>([]);
  const [editStaffPick, setEditStaffPick] = useState<StaffListItem | null>(null);

  const [resetPwdOpen, setResetPwdOpen] = useState(false);
  const [resetPwdUserId, setResetPwdUserId] = useState(0);
  const [resetPwdUsername, setResetPwdUsername] = useState("");
  const [resetPwdNew, setResetPwdNew] = useState("");
  const [savingReset, setSavingReset] = useState(false);

  const groupsForCourse = useMemo(
    () => groups.filter((g) => g.courseId === cuCourseId),
    [groups, cuCourseId],
  );

  const loadCatalog = useCallback(async () => {
    const res = await listPermissionCatalog();
    setPermCatalog(res.data);
  }, []);

  const loadRoles = useCallback(async () => {
    const res = await listApplicationRoles();
    setRoles(res.data);
  }, []);

  const loadUsers = useCallback(async () => {
    const res = await listTenantUsersRbac();
    setUsers(res.data);
  }, []);

  const refreshAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await Promise.all([loadCatalog(), loadRoles(), loadUsers()]);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [loadCatalog, loadRoles, loadUsers]);

  useEffect(() => {
    void refreshAll();
  }, [refreshAll]);

  const openCreateUser = async () => {
    setError(null);
    setCuUsername("");
    setCuPassword("");
    setCuLoginRole("Faculty");
    setCuAppRoles([]);
    setCuStaff(null);
    try {
      const [c, g, st] = await Promise.all([listMasterCourses(), listMasterGroups(), listStaff({ page: 1, pageSize: 500 })]);
      setCourses(c.data);
      setGroups(g.data);
      setStaffItems(st.data.items);
      const c0 = c.data[0]?.id ?? 0;
      setCuCourseId(c0);
      const g0 = g.data.find((x) => x.courseId === c0)?.id ?? g.data[0]?.id ?? 0;
      setCuGroupId(g0);
    } catch (e) {
      setError(errMsg(e));
      return;
    }
    setCreateUserOpen(true);
  };

  const saveCreateUser = async () => {
    const u = cuUsername.trim().toLowerCase();
    if (!u || !cuPassword || cuPassword.length < 8) {
      setError("Username and password (min 8 characters) are required.");
      return;
    }
    if (cuLoginRole === "Faculty") {
      if (!cuStaff) {
        setError("Select the staff profile this Faculty login represents.");
        return;
      }
    }
    setSavingCu(true);
    setError(null);
    setMessage(null);
    try {
      await createTenantUser({
        username: u,
        password: cuPassword,
        role: cuLoginRole,
        staffId: cuLoginRole === "Faculty" ? cuStaff!.id : undefined,
        courseId: cuLoginRole === "Faculty" && cuCourseId ? cuCourseId : undefined,
        groupId: cuLoginRole === "Faculty" && cuGroupId ? cuGroupId : undefined,
        applicationRoleIds: cuAppRoles.map((r) => r.id),
      });
      setMessage("User created. They must sign in and will be prompted to change the temporary password.");
      setCreateUserOpen(false);
      await loadUsers();
      await loadRoles();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSavingCu(false);
    }
  };

  const openResetPwd = (row: TenantUserRbacRow) => {
    setResetPwdUserId(row.id);
    setResetPwdUsername(row.username);
    setResetPwdNew("");
    setResetPwdOpen(true);
  };

  const saveResetPwd = async () => {
    if (resetPwdNew.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    setSavingReset(true);
    setError(null);
    setMessage(null);
    try {
      await adminResetUserPassword(resetPwdUserId, resetPwdNew);
      setMessage(`Temporary password set for ${resetPwdUsername}. User must sign in and change it.`);
      setResetPwdOpen(false);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSavingReset(false);
    }
  };

  const resourceGroups = useMemo(() => groupByResource(permCatalog), [permCatalog]);

  const openPermissions = async (r: ApplicationRoleListItem) => {
    setError(null);
    try {
      const detail = await getApplicationRole(r.id);
      setPermRoleId(r.id);
      setPermRoleLabel(`${r.name} (${r.code})`);
      setSelectedPermIds(new Set(detail.data.permissionIds));
      setPermOpen(true);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const togglePerm = (id: number) => {
    setSelectedPermIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const savePermissions = async () => {
    setSavingPerm(true);
    setError(null);
    setMessage(null);
    try {
      await setRolePermissions(permRoleId, [...selectedPermIds]);
      setMessage("Permissions updated. Users may need to sign in again to refresh their access.");
      setPermOpen(false);
      await loadRoles();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSavingPerm(false);
    }
  };

  const saveCreate = async () => {
    const n = createName.trim();
    const c = createCode.trim();
    if (!n || !c) {
      setError("Name and code are required.");
      return;
    }
    setSavingCreate(true);
    setError(null);
    setMessage(null);
    try {
      await createApplicationRole({
        name: n,
        code: c,
        description: createDesc.trim() || null,
      });
      setMessage("Role created.");
      setCreateOpen(false);
      setCreateName("");
      setCreateCode("");
      setCreateDesc("");
      await loadRoles();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSavingCreate(false);
    }
  };

  const openEditMeta = (r: ApplicationRoleListItem) => {
    setEditMetaId(r.id);
    setEditMetaName(r.name);
    setEditMetaDesc(r.description ?? "");
    setEditMetaOpen(true);
  };

  const saveEditMeta = async () => {
    const n = editMetaName.trim();
    if (!n) {
      setError("Name is required.");
      return;
    }
    setSavingMeta(true);
    setError(null);
    setMessage(null);
    try {
      await updateApplicationRole(editMetaId, { name: n, description: editMetaDesc.trim() || null });
      setMessage("Role updated.");
      setEditMetaOpen(false);
      await loadRoles();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSavingMeta(false);
    }
  };

  const removeRole = async (r: ApplicationRoleListItem) => {
    if (
      !window.confirm(
        `Delete role "${r.name}"? It must have no users assigned. Built-in ADMIN and FACULTY cannot be deleted.`,
      )
    )
      return;
    setError(null);
    setMessage(null);
    try {
      await deleteApplicationRole(r.id);
      setMessage("Role removed.");
      await Promise.all([loadRoles(), loadUsers()]);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const openUserEdit = async (u: TenantUserRbacRow) => {
    setUserEditRow(u);
    const picked = roles.filter((r) => u.applicationRoleIds.includes(r.id));
    setUserRolePick(picked);
    setEditStaffPick(null);
    setStaffItemsEdit([]);
    if (u.enumRole === "Faculty") {
      try {
        const res = await listStaff({ page: 1, pageSize: 500 });
        setStaffItemsEdit(res.data.items);
        if (u.staffId != null && u.staffId > 0) {
          setEditStaffPick(res.data.items.find((s) => s.id === u.staffId) ?? null);
        }
      } catch {
        setStaffItemsEdit([]);
      }
    }
    setUserEditOpen(true);
  };

  const saveUserRoles = async () => {
    if (!userEditRow) return;
    setSavingUser(true);
    setError(null);
    setMessage(null);
    try {
      await setUserApplicationRoles(
        userEditRow.id,
        userRolePick.map((r) => r.id),
      );
      if (userEditRow.enumRole === "Faculty" && staffItemsEdit.length > 0) {
        await linkUserStaff(userEditRow.id, editStaffPick?.id ?? null);
      }
      setMessage("Assignments saved. Users must sign out and sign in again for changes to apply.");
      setUserEditOpen(false);
      await loadUsers();
      await loadRoles();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSavingUser(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
          Catalog
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Roles &amp; permissions
        </Typography>
        {tab === 0 && (
          <Button variant="contained" onClick={() => setCreateOpen(true)}>
            New role
          </Button>
        )}
        {tab === 1 && (
          <Button variant="contained" onClick={() => void openCreateUser()}>
            Add user
          </Button>
        )}
      </Box>

      <Typography variant="body2" color="text.secondary">
        Define application roles for your college and assign them to user accounts. Login type (Admin / Faculty) still
        applies; when a user has application roles, JWT permissions come from those roles until you clear them.
      </Typography>

      <Tabs value={tab} onChange={(_, v) => setTab(v)}>
        <Tab label="Application roles" />
        <Tab label="User assignments" />
      </Tabs>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : tab === 0 ? (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Code</TableCell>
              <TableCell align="right">Permissions</TableCell>
              <TableCell align="right">Users</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {roles.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.code}</TableCell>
                <TableCell align="right">{r.permissionCount}</TableCell>
                <TableCell align="right">{r.assignedUserCount}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => openEditMeta(r)}>
                    Rename
                  </Button>{" "}
                  <Button size="small" onClick={() => void openPermissions(r)}>
                    Permissions
                  </Button>{" "}
                  <Button size="small" color="error" onClick={() => void removeRole(r)}>
                    Delete
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Username</TableCell>
              <TableCell>Login role</TableCell>
              <TableCell>Staff</TableCell>
              <TableCell>Application roles</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((u) => (
              <TableRow key={u.id} hover>
                <TableCell>{u.username}</TableCell>
                <TableCell>{u.enumRole}</TableCell>
                <TableCell>
                  {u.enumRole === "Faculty" && u.staffId != null && u.staffId > 0 ? (
                    <Typography variant="body2">#{u.staffId}</Typography>
                  ) : (
                    <Typography variant="body2" color="text.secondary">
                      —
                    </Typography>
                  )}
                </TableCell>
                <TableCell>
                  {u.applicationRoleIds.length === 0 ? (
                    <Typography variant="body2" color="text.secondary">
                      (legacy — enum defaults)
                    </Typography>
                  ) : (
                    <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
                      {u.applicationRoleIds.map((rid) => {
                        const name = roles.find((x) => x.id === rid)?.code ?? `#${rid}`;
                        return <Chip key={rid} size="small" label={name} variant="outlined" />;
                      })}
                    </Box>
                  )}
                </TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => openUserEdit(u)}>
                    Assign roles
                  </Button>{" "}
                  <Button
                    size="small"
                    onClick={() => openResetPwd(u)}
                    disabled={u.id === myUserId}
                    title={u.id === myUserId ? "Use Change password in the header for your account" : undefined}
                  >
                    Reset password
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={createOpen} onClose={() => !savingCreate && setCreateOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>New application role</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Name" value={createName} onChange={(e) => setCreateName(e.target.value)} fullWidth required />
            <TextField
              label="Code"
              value={createCode}
              onChange={(e) => setCreateCode(e.target.value.toUpperCase())}
              fullWidth
              required
              helperText="Uppercase, unique in your college (e.g. EXAM_OFFICER)."
            />
            <TextField
              label="Description (optional)"
              value={createDesc}
              onChange={(e) => setCreateDesc(e.target.value)}
              fullWidth
              multiline
              minRows={2}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)} disabled={savingCreate}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void saveCreate()} disabled={savingCreate}>
            {savingCreate ? "Saving…" : "Create"}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={editMetaOpen} onClose={() => !savingMeta && setEditMetaOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Edit role</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Name" value={editMetaName} onChange={(e) => setEditMetaName(e.target.value)} fullWidth required />
            <TextField
              label="Description (optional)"
              value={editMetaDesc}
              onChange={(e) => setEditMetaDesc(e.target.value)}
              fullWidth
              multiline
              minRows={2}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditMetaOpen(false)} disabled={savingMeta}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void saveEditMeta()} disabled={savingMeta}>
            {savingMeta ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={permOpen} onClose={() => !savingPerm && setPermOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>Permissions — {permRoleLabel}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Select capabilities granted when this role is assigned to a user.
          </Typography>
          <Stack spacing={2}>
            {resourceGroups.map(([resource, perms]) => (
              <Box key={resource}>
                <Typography variant="subtitle2" sx={{ mb: 1 }}>
                  {resource}
                </Typography>
                <FormGroup>
                  {perms.map((p) => (
                    <FormControlLabel
                      key={p.id}
                      control={
                        <Checkbox checked={selectedPermIds.has(p.id)} onChange={() => togglePerm(p.id)} size="small" />
                      }
                      label={`${p.action} — ${p.key}`}
                    />
                  ))}
                </FormGroup>
              </Box>
            ))}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPermOpen(false)} disabled={savingPerm}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void savePermissions()} disabled={savingPerm}>
            {savingPerm ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={userEditOpen} onClose={() => !savingUser && setUserEditOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Assign roles — {userEditRow?.username}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Login role stays <strong>{userEditRow?.enumRole}</strong>. Application roles drive JWT permission claims when at
            least one is selected.
          </Typography>
          {userEditRow?.enumRole === "Faculty" && (
            <Autocomplete
              sx={{ mb: 2 }}
              options={staffItemsEdit}
              getOptionLabel={(o) =>
                `${o.firstName} ${o.lastName}${o.staffCode ? ` (${o.staffCode})` : ""}`
              }
              value={editStaffPick}
              onChange={(_, v) => setEditStaffPick(v)}
              disabled={staffItemsEdit.length === 0}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Staff profile"
                  helperText={
                    staffItemsEdit.length === 0
                      ? "Staff list failed to load — reopen this dialog or refresh the page."
                      : "Which directory row defines teaching-subject access for this Faculty login."
                  }
                />
              )}
            />
          )}
          <Autocomplete
            multiple
            options={roles}
            getOptionLabel={(o) => `${o.name} (${o.code})`}
            value={userRolePick}
            onChange={(_, v) => setUserRolePick(v)}
            renderInput={(params) => <TextField {...params} label="Application roles" placeholder="Select roles" />}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setUserEditOpen(false)} disabled={savingUser}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void saveUserRoles()} disabled={savingUser}>
            {savingUser ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={createUserOpen} onClose={() => !savingCu && setCreateUserOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Add user</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Username"
              value={cuUsername}
              onChange={(e) => setCuUsername(e.target.value)}
              fullWidth
              required
              autoComplete="off"
            />
            <TextField
              label="Temporary password"
              type="password"
              value={cuPassword}
              onChange={(e) => setCuPassword(e.target.value)}
              fullWidth
              required
              helperText="Min 8 characters. User will be asked to change it on first sign-in."
            />
            <FormControl fullWidth>
              <InputLabel id="login-role-label">Login role</InputLabel>
              <Select
                labelId="login-role-label"
                label="Login role"
                value={cuLoginRole}
                onChange={(e) => setCuLoginRole(e.target.value as "Admin" | "Faculty")}
              >
                <MenuItem value="Admin">Admin</MenuItem>
                <MenuItem value="Faculty">Faculty</MenuItem>
              </Select>
            </FormControl>
            {cuLoginRole === "Faculty" && (
              <>
                <Autocomplete
                  options={staffItems}
                  getOptionLabel={(o) =>
                    `${o.firstName} ${o.lastName}${o.staffCode ? ` (${o.staffCode})` : ""}`
                  }
                  value={cuStaff}
                  onChange={(_, v) => setCuStaff(v)}
                  renderInput={(params) => (
                    <TextField
                      {...params}
                      label="Staff profile"
                      required
                      helperText="Access is limited to subjects assigned to this staff member. If they have no subjects yet, pick course and section below."
                    />
                  )}
                />
                <FormControl fullWidth>
                  <InputLabel id="cu-course-label">Course (when staff has no subjects yet)</InputLabel>
                  <Select
                    labelId="cu-course-label"
                    label="Course"
                    value={cuCourseId || ""}
                    onChange={(e) => {
                      const id = Number(e.target.value);
                      setCuCourseId(id);
                      const g = groups.find((x) => x.courseId === id);
                      if (g) setCuGroupId(g.id);
                    }}
                  >
                    {courses.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {c.name} ({c.code})
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
                <FormControl fullWidth>
                  <InputLabel id="cu-group-label">Section (when staff has no subjects yet)</InputLabel>
                  <Select
                    labelId="cu-group-label"
                    label="Section (when staff has no subjects yet)"
                    value={cuGroupId || ""}
                    onChange={(e) => setCuGroupId(Number(e.target.value))}
                  >
                    {groupsForCourse.map((g) => (
                      <MenuItem key={g.id} value={g.id}>
                        {g.name} ({g.code})
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              </>
            )}
            <Autocomplete
              multiple
              options={roles}
              getOptionLabel={(o) => `${o.name} (${o.code})`}
              value={cuAppRoles}
              onChange={(_, v) => setCuAppRoles(v)}
              renderInput={(params) => (
                <TextField {...params} label="Application roles (optional)" placeholder="Select roles" />
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateUserOpen(false)} disabled={savingCu}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void saveCreateUser()} disabled={savingCu}>
            {savingCu ? "Creating…" : "Create user"}
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={resetPwdOpen} onClose={() => !savingReset && setResetPwdOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Reset password — {resetPwdUsername}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2, mt: 1 }}>
            Sets a new temporary password and requires the user to change it when they next sign in.
          </Typography>
          <TextField
            label="New temporary password"
            type="password"
            value={resetPwdNew}
            onChange={(e) => setResetPwdNew(e.target.value)}
            fullWidth
            helperText="At least 8 characters."
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetPwdOpen(false)} disabled={savingReset}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void saveResetPwd()} disabled={savingReset}>
            {savingReset ? "Saving…" : "Set password"}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default TenantRbacPage;
