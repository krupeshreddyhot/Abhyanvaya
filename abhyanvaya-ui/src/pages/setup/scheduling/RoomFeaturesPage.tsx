import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert,
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
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import { Controller, useForm } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  assignRoomFeature,
  cloneRoomFeatureAssignments,
  createRoomFeature,
  deleteRoomFeature,
  listRoomFeatureAssignments,
  listRoomFeatures,
  searchRooms,
  unassignRoomFeature,
  updateRoomFeature,
  type CloneRoomFeatureAssignmentsRequest,
  type CreateRoomFeatureRequest,
  type RoomFeatureAssignmentDto,
  type RoomFeatureDto,
} from "../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";

type FeatureForm = CreateRoomFeatureRequest;

const RoomFeaturesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingRoomFeaturesManage);

  const [features, setFeatures] = useState<RoomFeatureDto[]>([]);
  const [rooms, setRooms] = useState<{ id: number; label: string }[]>([]);
  const [categoryFilter, setCategoryFilter] = useState<string>("");
  const [selectedRoomId, setSelectedRoomId] = useState<number | "">("");
  const [assignments, setAssignments] = useState<RoomFeatureAssignmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [assignLoading, setAssignLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [featureDialogOpen, setFeatureDialogOpen] = useState(false);
  const [cloneDialogOpen, setCloneDialogOpen] = useState(false);
  const [editingFeatureId, setEditingFeatureId] = useState(0);
  const [saving, setSaving] = useState(false);

  const featureForm = useForm<FeatureForm>({
    defaultValues: { code: "", name: "", category: "", sortOrder: 0, isActive: true },
  });

  const cloneForm = useForm<CloneRoomFeatureAssignmentsRequest>({
    defaultValues: { fromRoomId: 0, toRoomId: 0 },
  });

  const categories = useMemo(
    () => [...new Set(features.map((f) => f.category))].sort(),
    [features],
  );

  const filteredFeatures = useMemo(
    () => (categoryFilter ? features.filter((f) => f.category === categoryFilter) : features),
    [features, categoryFilter],
  );

  const assignedFeatureIds = useMemo(
    () => new Set(assignments.map((a) => a.roomFeatureId)),
    [assignments],
  );

  const loadFeatures = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listRoomFeatures();
      setFeatures(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, []);

  const loadRooms = useCallback(async () => {
    try {
      const res = await searchRooms({ pageSize: 500, isActive: true });
      setRooms(res.data.items.map((r) => ({ id: r.id, label: `${r.code} — ${r.name}` })));
    } catch (e) {
      setError(errMsg(e));
    }
  }, []);

  const loadAssignments = useCallback(async (roomId: number) => {
    setAssignLoading(true);
    try {
      const res = await listRoomFeatureAssignments(roomId);
      setAssignments(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setAssignLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadFeatures();
    void loadRooms();
  }, [loadFeatures, loadRooms]);

  useEffect(() => {
    if (selectedRoomId === "") {
      setAssignments([]);
      return;
    }
    void loadAssignments(selectedRoomId);
  }, [selectedRoomId, loadAssignments]);

  const openAddFeature = () => {
    setEditingFeatureId(0);
    featureForm.reset({ code: "", name: "", category: categories[0] ?? "", sortOrder: features.length, isActive: true });
    setFeatureDialogOpen(true);
  };

  const openEditFeature = (f: RoomFeatureDto) => {
    setEditingFeatureId(f.id);
    featureForm.reset({ code: f.code, name: f.name, category: f.category, sortOrder: f.sortOrder, isActive: f.isActive });
    setFeatureDialogOpen(true);
  };

  const saveFeature = featureForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingFeatureId) await updateRoomFeature(editingFeatureId, { ...values, id: editingFeatureId });
      else await createRoomFeature(values);
      setMessage(editingFeatureId ? "Feature updated." : "Feature created.");
      setFeatureDialogOpen(false);
      await loadFeatures();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDeleteFeature = async (id: number) => {
    if (!window.confirm("Delete this room feature from the catalog?")) return;
    try {
      await deleteRoomFeature(id);
      setMessage("Feature deleted.");
      await loadFeatures();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handleAssign = async (featureId: number) => {
    if (selectedRoomId === "" || !canManage) return;
    try {
      await assignRoomFeature(selectedRoomId, { roomFeatureId: featureId });
      setMessage("Feature assigned to room.");
      await loadAssignments(selectedRoomId);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handleUnassign = async (featureId: number) => {
    if (selectedRoomId === "" || !canManage) return;
    try {
      await unassignRoomFeature(selectedRoomId, featureId);
      setMessage("Feature removed from room.");
      await loadAssignments(selectedRoomId);
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const saveClone = cloneForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      await cloneRoomFeatureAssignments(values);
      setMessage("Room feature assignments cloned.");
      setCloneDialogOpen(false);
      if (selectedRoomId === values.toRoomId) await loadAssignments(values.toRoomId);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Room features
        </Typography>
        {canManage && (
          <>
            <Button variant="outlined" startIcon={<ContentCopyIcon />} onClick={() => { cloneForm.reset({ fromRoomId: rooms[0]?.id ?? 0, toRoomId: rooms[1]?.id ?? 0 }); setCloneDialogOpen(true); }} disabled={rooms.length < 2}>
              Clone assignments
            </Button>
            <Button variant="contained" onClick={openAddFeature}>
              Add feature
            </Button>
          </>
        )}
      </Box>

      <Typography variant="body2" color="text.secondary">
        Manage the room feature catalog and assign features to individual rooms. Select a room below to toggle feature chips.
      </Typography>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
        <Box sx={{ flex: 1 }}>
          <Stack direction="row" spacing={2} sx={{ mb: 1 }}>
            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="cat">Category</InputLabel>
              <Select labelId="cat" label="Category" value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}>
                <MenuItem value="">All</MenuItem>
                {categories.map((c) => (
                  <MenuItem key={c} value={c}>
                    {c}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>

          {loading ? (
            <CircularProgress />
          ) : (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Code</TableCell>
                  <TableCell>Name</TableCell>
                  <TableCell>Category</TableCell>
                  <TableCell>Sort</TableCell>
                  <TableCell>Active</TableCell>
                  {canManage && <TableCell align="right">Actions</TableCell>}
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredFeatures.map((f) => (
                  <TableRow key={f.id} hover>
                    <TableCell>{f.code}</TableCell>
                    <TableCell>{f.name}</TableCell>
                    <TableCell>{f.category}</TableCell>
                    <TableCell>{f.sortOrder}</TableCell>
                    <TableCell>{f.isActive ? "Yes" : "No"}</TableCell>
                    {canManage && (
                      <TableCell align="right">
                        <Button size="small" onClick={() => openEditFeature(f)}>
                          Edit
                        </Button>
                        <Button size="small" color="error" onClick={() => void handleDeleteFeature(f.id)}>
                          Delete
                        </Button>
                      </TableCell>
                    )}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>

        <Box sx={{ flex: 1, minWidth: 280 }}>
          <Typography variant="subtitle1" gutterBottom>
            Room assignments
          </Typography>
          <FormControl fullWidth size="small" sx={{ mb: 2 }}>
            <InputLabel id="room">Room</InputLabel>
            <Select
              labelId="room"
              label="Room"
              value={selectedRoomId}
              onChange={(e) => setSelectedRoomId(parseOptionalSelectNumber(e.target.value))}
            >
              <MenuItem value="">Select a room</MenuItem>
              {rooms.map((r) => (
                <MenuItem key={r.id} value={r.id}>
                  {r.label}
                </MenuItem>
              ))}
            </Select>
          </FormControl>

          {selectedRoomId === "" ? (
            <Typography variant="body2" color="text.secondary">
              Choose a room to view and assign features.
            </Typography>
          ) : assignLoading ? (
            <CircularProgress size={24} />
          ) : (
            <Stack spacing={1}>
              <Typography variant="body2" color="text.secondary">
                Click chips to assign or remove features for this room.
              </Typography>
              <Box sx={{ display: "flex", flexWrap: "wrap", gap: 1 }}>
                {features
                  .filter((f) => f.isActive)
                  .map((f) => {
                    const assigned = assignedFeatureIds.has(f.id);
                    return (
                      <Chip
                        key={f.id}
                        label={`${f.code} — ${f.name}`}
                        color={assigned ? "primary" : "default"}
                        variant={assigned ? "filled" : "outlined"}
                        onClick={() => void (assigned ? handleUnassign(f.id) : handleAssign(f.id))}
                        disabled={!canManage}
                      />
                    );
                  })}
              </Box>
              {assignments.length > 0 && (
                <Table size="small" sx={{ mt: 2 }}>
                  <TableHead>
                    <TableRow>
                      <TableCell>Feature</TableCell>
                      <TableCell>Category</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {assignments.map((a) => (
                      <TableRow key={a.id}>
                        <TableCell>
                          {a.featureCode} — {a.featureName}
                        </TableCell>
                        <TableCell>{a.featureCategory}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </Stack>
          )}
        </Box>
      </Stack>

      <Dialog open={featureDialogOpen} onClose={() => !saving && setFeatureDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingFeatureId ? "Edit room feature" : "Add room feature"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="feature-form" onSubmit={saveFeature}>
            <Controller name="code" control={featureForm.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
            <Controller name="name" control={featureForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller name="category" control={featureForm.control} render={({ field }) => <TextField {...field} label="Category" fullWidth required />} />
            <Controller name="sortOrder" control={featureForm.control} render={({ field }) => <TextField {...field} label="Sort order" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="isActive" control={featureForm.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFeatureDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="feature-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={cloneDialogOpen} onClose={() => !saving && setCloneDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Clone room feature assignments</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="clone-form" onSubmit={saveClone}>
            <Controller
              name="fromRoomId"
              control={cloneForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="from">From room</InputLabel>
                  <Select labelId="from" label="From room" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {rooms.map((r) => (
                      <MenuItem key={r.id} value={r.id}>
                        {r.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="toRoomId"
              control={cloneForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="to">To room</InputLabel>
                  <Select labelId="to" label="To room" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {rooms.map((r) => (
                      <MenuItem key={r.id} value={r.id}>
                        {r.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCloneDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="clone-form" variant="contained" disabled={saving}>
            Clone
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default RoomFeaturesPage;
