import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
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
  Switch,
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
import { Controller, useForm } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  createBuilding,
  createCampus,
  createFloor,
  deleteBuilding,
  deleteCampus,
  deleteFloor,
  listBuildings,
  listCampuses,
  listFloors,
  updateBuilding,
  updateCampus,
  updateFloor,
  type BuildingDto,
  type CampusDto,
  type CreateBuildingRequest,
  type CreateCampusRequest,
  type CreateFloorRequest,
  type FloorDto,
} from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";

const CampusFacilitiesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [tab, setTab] = useState(0);
  const [campuses, setCampuses] = useState<CampusDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [floors, setFloors] = useState<FloorDto[]>([]);
  const [campusFilter, setCampusFilter] = useState<number>(0);
  const [buildingFilter, setBuildingFilter] = useState<number>(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogKind, setDialogKind] = useState<"campus" | "building" | "floor">("campus");
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const campusForm = useForm<CreateCampusRequest>({ defaultValues: { name: "", code: "", address: "", isActive: true } });
  const buildingForm = useForm<CreateBuildingRequest>({ defaultValues: { campusId: 0, name: "", code: "", isActive: true } });
  const floorForm = useForm<CreateFloorRequest>({ defaultValues: { buildingId: 0, name: "", levelNumber: 0 } });

  const loadCampuses = useCallback(async () => {
    const res = await listCampuses();
    setCampuses(res.data);
    if (!campusFilter && res.data[0]) setCampusFilter(res.data[0].id);
  }, [campusFilter]);

  const loadBuildings = useCallback(async () => {
    const res = await listBuildings(campusFilter || undefined);
    setBuildings(res.data);
    if (!buildingFilter && res.data[0]) setBuildingFilter(res.data[0].id);
  }, [campusFilter, buildingFilter]);

  const loadFloors = useCallback(async () => {
    const res = await listFloors(buildingFilter || undefined);
    setFloors(res.data);
  }, [buildingFilter]);

  const loadAll = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      await loadCampuses();
      await loadBuildings();
      await loadFloors();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [loadCampuses, loadBuildings, loadFloors]);

  useEffect(() => {
    void loadAll();
  }, [loadAll]);

  useEffect(() => {
    void loadBuildings();
  }, [campusFilter, loadBuildings]);

  useEffect(() => {
    void loadFloors();
  }, [buildingFilter, loadFloors]);

  const openCampusDialog = (r?: CampusDto) => {
    setDialogKind("campus");
    setEditingId(r?.id ?? 0);
    campusForm.reset(r ? { name: r.name, code: r.code, address: r.address ?? "", isActive: r.isActive } : { name: "", code: "", address: "", isActive: true });
    setDialogOpen(true);
  };

  const openBuildingDialog = (r?: BuildingDto) => {
    setDialogKind("building");
    setEditingId(r?.id ?? 0);
    buildingForm.reset(
      r
        ? { campusId: r.campusId, name: r.name, code: r.code, isActive: r.isActive }
        : { campusId: campusFilter, name: "", code: "", isActive: true },
    );
    setDialogOpen(true);
  };

  const openFloorDialog = (r?: FloorDto) => {
    setDialogKind("floor");
    setEditingId(r?.id ?? 0);
    floorForm.reset(r ? { buildingId: r.buildingId, name: r.name, levelNumber: r.levelNumber } : { buildingId: buildingFilter, name: "", levelNumber: 0 });
    setDialogOpen(true);
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (dialogKind === "campus") {
        const v = campusForm.getValues();
        if (editingId) await updateCampus(editingId, { ...v, id: editingId, address: v.address || null });
        else await createCampus({ ...v, address: v.address || null });
      } else if (dialogKind === "building") {
        const v = buildingForm.getValues();
        if (editingId) await updateBuilding(editingId, { ...v, id: editingId });
        else await createBuilding(v);
      } else {
        const v = floorForm.getValues();
        if (editingId) await updateFloor(editingId, { ...v, id: editingId });
        else await createFloor(v);
      }
      setMessage(`${dialogKind.charAt(0).toUpperCase()}${dialogKind.slice(1)} saved.`);
      setDialogOpen(false);
      await loadAll();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (kind: "campus" | "building" | "floor", id: number) => {
    if (!window.confirm("Delete this record?")) return;
    try {
      if (kind === "campus") await deleteCampus(id);
      else if (kind === "building") await deleteBuilding(id);
      else await deleteFloor(id);
      setMessage("Deleted.");
      await loadAll();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Campus facilities
        </Typography>
      </Box>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      <Tabs value={tab} onChange={(_, v) => setTab(v)}>
        <Tab label="Campuses" />
        <Tab label="Buildings" />
        <Tab label="Floors" />
      </Tabs>

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <>
          {tab === 0 && (
            <Stack spacing={1}>
              {canManage && (
                <Button variant="contained" onClick={() => openCampusDialog()} sx={{ alignSelf: "flex-start" }}>
                  Add campus
                </Button>
              )}
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Code</TableCell>
                    <TableCell>Name</TableCell>
                    <TableCell>Address</TableCell>
                    <TableCell>Active</TableCell>
                    {canManage && <TableCell align="right">Actions</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {campuses.map((r) => (
                    <TableRow key={r.id} hover>
                      <TableCell>{r.code}</TableCell>
                      <TableCell>{r.name}</TableCell>
                      <TableCell>{r.address ?? "—"}</TableCell>
                      <TableCell>{r.isActive ? "Yes" : "No"}</TableCell>
                      {canManage && (
                        <TableCell align="right">
                          <Button size="small" onClick={() => openCampusDialog(r)}>
                            Edit
                          </Button>
                          <Button size="small" color="error" onClick={() => void handleDelete("campus", r.id)}>
                            Delete
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Stack>
          )}

          {tab === 1 && (
            <Stack spacing={1}>
              <FormControl size="small" sx={{ minWidth: 220 }}>
                <InputLabel id="campus-filter">Campus</InputLabel>
                <Select labelId="campus-filter" label="Campus" value={campusFilter || ""} onChange={(e) => setCampusFilter(Number(e.target.value))}>
                  {campuses.map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {canManage && (
                <Button variant="contained" onClick={() => openBuildingDialog()} sx={{ alignSelf: "flex-start" }}>
                  Add building
                </Button>
              )}
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Code</TableCell>
                    <TableCell>Name</TableCell>
                    <TableCell>Active</TableCell>
                    {canManage && <TableCell align="right">Actions</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {buildings.map((r) => (
                    <TableRow key={r.id} hover>
                      <TableCell>{r.code}</TableCell>
                      <TableCell>{r.name}</TableCell>
                      <TableCell>{r.isActive ? "Yes" : "No"}</TableCell>
                      {canManage && (
                        <TableCell align="right">
                          <Button size="small" onClick={() => openBuildingDialog(r)}>
                            Edit
                          </Button>
                          <Button size="small" color="error" onClick={() => void handleDelete("building", r.id)}>
                            Delete
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Stack>
          )}

          {tab === 2 && (
            <Stack spacing={1}>
              <FormControl size="small" sx={{ minWidth: 220 }}>
                <InputLabel id="building-filter">Building</InputLabel>
                <Select labelId="building-filter" label="Building" value={buildingFilter || ""} onChange={(e) => setBuildingFilter(Number(e.target.value))}>
                  {buildings.map((b) => (
                    <MenuItem key={b.id} value={b.id}>
                      {b.code} — {b.name}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
              {canManage && (
                <Button variant="contained" onClick={() => openFloorDialog()} sx={{ alignSelf: "flex-start" }}>
                  Add floor
                </Button>
              )}
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Name</TableCell>
                    <TableCell>Level</TableCell>
                    {canManage && <TableCell align="right">Actions</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {floors.map((r) => (
                    <TableRow key={r.id} hover>
                      <TableCell>{r.name}</TableCell>
                      <TableCell>{r.levelNumber}</TableCell>
                      {canManage && (
                        <TableCell align="right">
                          <Button size="small" onClick={() => openFloorDialog(r)}>
                            Edit
                          </Button>
                          <Button size="small" color="error" onClick={() => void handleDelete("floor", r.id)}>
                            Delete
                          </Button>
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </Stack>
          )}
        </>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>
          {editingId ? "Edit" : "Add"} {dialogKind}
        </DialogTitle>
        <DialogContent>
          {dialogKind === "campus" && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Controller name="code" control={campusForm.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
              <Controller name="name" control={campusForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
              <Controller name="address" control={campusForm.control} render={({ field }) => <TextField {...field} label="Address" fullWidth />} />
              <Controller
                name="isActive"
                control={campusForm.control}
                render={({ field }) => <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />}
              />
            </Stack>
          )}
          {dialogKind === "building" && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Controller
                name="campusId"
                control={buildingForm.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel id="b-campus">Campus</InputLabel>
                    <Select labelId="b-campus" label="Campus" value={field.value || ""} onChange={(e) => field.onChange(Number(e.target.value))}>
                      {campuses.map((c) => (
                        <MenuItem key={c.id} value={c.id}>
                          {c.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
              <Controller name="code" control={buildingForm.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
              <Controller name="name" control={buildingForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
              <Controller
                name="isActive"
                control={buildingForm.control}
                render={({ field }) => <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />}
              />
            </Stack>
          )}
          {dialogKind === "floor" && (
            <Stack spacing={2} sx={{ mt: 1 }}>
              <Controller
                name="buildingId"
                control={floorForm.control}
                render={({ field }) => (
                  <FormControl fullWidth>
                    <InputLabel id="f-building">Building</InputLabel>
                    <Select labelId="f-building" label="Building" value={field.value || ""} onChange={(e) => field.onChange(Number(e.target.value))}>
                      {buildings.map((b) => (
                        <MenuItem key={b.id} value={b.id}>
                          {b.name}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                )}
              />
              <Controller name="name" control={floorForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
              <Controller
                name="levelNumber"
                control={floorForm.control}
                render={({ field }) => (
                  <TextField {...field} label="Level number" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />
                )}
              />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void save()} disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default CampusFacilitiesPage;
