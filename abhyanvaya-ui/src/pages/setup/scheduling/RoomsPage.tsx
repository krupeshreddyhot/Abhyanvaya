import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
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
  RoomFeatureFlags,
  RoomStatus,
  RoomType,
  createRoom,
  deleteRoom,
  listBuildings,
  listCampuses,
  listFloors,
  searchRooms,
  updateRoom,
  type CreateRoomRequest,
  type RoomDto,
} from "../../../services/schedulingService";
import { errMsg, parseOptionalSelectNumber } from "./schedulingFormUtils";

const ROOM_TYPE_LABELS: Record<number, string> = {
  [RoomType.Classroom]: "Classroom",
  [RoomType.ComputerLab]: "Computer lab",
  [RoomType.ScienceLab]: "Science lab",
  [RoomType.CommerceLab]: "Commerce lab",
  [RoomType.Seminar]: "Seminar",
  [RoomType.Auditorium]: "Auditorium",
  [RoomType.Other]: "Other",
};

const ROOM_STATUS_LABELS: Record<number, string> = {
  [RoomStatus.Available]: "Available",
  [RoomStatus.Maintenance]: "Maintenance",
  [RoomStatus.Reserved]: "Reserved",
};

const FEATURE_OPTIONS = [
  { flag: RoomFeatureFlags.AiCamera, label: "AI camera" },
  { flag: RoomFeatureFlags.Projector, label: "Projector" },
  { flag: RoomFeatureFlags.Wifi, label: "Wi-Fi" },
  { flag: RoomFeatureFlags.SmartBoard, label: "Smart board" },
  { flag: RoomFeatureFlags.SmartClassroom, label: "Smart classroom" },
];

const RoomsPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [rows, setRows] = useState<RoomDto[]>([]);
  const [total, setTotal] = useState(0);
  const [campuses, setCampuses] = useState<{ id: number; name: string }[]>([]);
  const [buildings, setBuildings] = useState<{ id: number; name: string }[]>([]);
  const [floors, setFloors] = useState<{ id: number; name: string }[]>([]);
  const [search, setSearch] = useState("");
  const [campusId, setCampusId] = useState<number | "">("");
  const [buildingId, setBuildingId] = useState<number | "">("");
  const [floorId, setFloorId] = useState<number | "">("");
  const [roomType, setRoomType] = useState<number | "">("");
  const [status, setStatus] = useState<number | "">("");
  const [sortBy, setSortBy] = useState("name");
  const [sortDesc, setSortDesc] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<CreateRoomRequest>({
    defaultValues: {
      floorId: 0,
      name: "",
      code: "",
      roomType: RoomType.Classroom,
      capacity: 30,
      status: RoomStatus.Available,
      featureFlags: RoomFeatureFlags.None,
      departmentId: null,
      isActive: true,
    },
  });

  useEffect(() => {
    void (async () => {
      try {
        const [c, b, f] = await Promise.all([listCampuses(), listBuildings(), listFloors()]);
        setCampuses(c.data.map((x) => ({ id: x.id, name: x.name })));
        setBuildings(b.data.map((x) => ({ id: x.id, name: x.name })));
        setFloors(f.data.map((x) => ({ id: x.id, name: x.name })));
      } catch (e) {
        setError(errMsg(e));
      }
    })();
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await searchRooms({
        search: search || undefined,
        campusId: campusId || undefined,
        buildingId: buildingId || undefined,
        floorId: floorId || undefined,
        roomType: (roomType || undefined) as RoomType | undefined,
        status: (status || undefined) as RoomStatus | undefined,
        sortBy,
        sortDescending: sortDesc,
        page: 1,
        pageSize: 100,
      });
      setRows(res.data.items);
      setTotal(res.data.totalCount);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, [search, campusId, buildingId, floorId, roomType, status, sortBy, sortDesc]);

  useEffect(() => {
    void load();
  }, [load]);

  const openAdd = () => {
    setEditingId(0);
    form.reset({
      floorId: typeof floorId === "number" ? floorId : floors[0]?.id ?? 0,
      name: "",
      code: "",
      roomType: RoomType.Classroom,
      capacity: 30,
      status: RoomStatus.Available,
      featureFlags: RoomFeatureFlags.None,
      departmentId: null,
      isActive: true,
    });
    setDialogOpen(true);
  };

  const openEdit = (r: RoomDto) => {
    setEditingId(r.id);
    form.reset({
      floorId: r.floorId,
      name: r.name,
      code: r.code,
      roomType: r.roomType,
      capacity: r.capacity,
      status: r.status,
      featureFlags: r.featureFlags,
      departmentId: r.departmentId,
      isActive: r.isActive,
    });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) await updateRoom(editingId, { ...values, id: editingId });
      else await createRoom(values);
      setMessage(editingId ? "Room updated." : "Room created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this room?")) return;
    try {
      await deleteRoom(id);
      setMessage("Room deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const featureFlags = form.watch("featureFlags");

  const toggleFeature = (flag: number) => {
    const current = form.getValues("featureFlags");
    form.setValue("featureFlags", (current & flag) === flag ? current & ~flag : current | flag);
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/setup/scheduling" startIcon={<ArrowBackIcon />} variant="text">
          Scheduling
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Rooms
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add room
          </Button>
        )}
      </Box>

      <Stack direction={{ xs: "column", md: "row" }} spacing={1} sx={{ flexWrap: "wrap" }} useFlexGap>
        <TextField size="small" label="Search" value={search} onChange={(e) => setSearch(e.target.value)} sx={{ minWidth: 160 }} />
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="campus">Campus</InputLabel>
          <Select labelId="campus" label="Campus" value={campusId === "" ? "" : campusId} onChange={(e) => setCampusId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {campuses.map((c) => (
              <MenuItem key={c.id} value={c.id}>
                {c.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 140 }}>
          <InputLabel id="building">Building</InputLabel>
          <Select labelId="building" label="Building" value={buildingId === "" ? "" : buildingId} onChange={(e) => setBuildingId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {buildings.map((b) => (
              <MenuItem key={b.id} value={b.id}>
                {b.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel id="floor">Floor</InputLabel>
          <Select labelId="floor" label="Floor" value={floorId === "" ? "" : floorId} onChange={(e) => setFloorId(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {floors.map((f) => (
              <MenuItem key={f.id} value={f.id}>
                {f.name}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel id="rtype">Type</InputLabel>
          <Select labelId="rtype" label="Type" value={roomType === "" ? "" : roomType} onChange={(e) => setRoomType(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {Object.entries(ROOM_TYPE_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>
                {v}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel id="rstatus">Status</InputLabel>
          <Select labelId="rstatus" label="Status" value={status === "" ? "" : status} onChange={(e) => setStatus(parseOptionalSelectNumber(e.target.value))}>
            <MenuItem value="">All</MenuItem>
            {Object.entries(ROOM_STATUS_LABELS).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>
                {v}
              </MenuItem>
            ))}
          </Select>
        </FormControl>
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel id="sort">Sort by</InputLabel>
          <Select labelId="sort" label="Sort by" value={sortBy} onChange={(e) => setSortBy(e.target.value)}>
            <MenuItem value="name">Name</MenuItem>
            <MenuItem value="code">Code</MenuItem>
            <MenuItem value="capacity">Capacity</MenuItem>
          </Select>
        </FormControl>
        <FormControlLabel control={<Checkbox checked={sortDesc} onChange={(_, v) => setSortDesc(v)} />} label="Desc" />
        <Button variant="outlined" onClick={() => void load()}>
          Apply
        </Button>
      </Stack>

      <Typography variant="body2" color="text.secondary">
        {total} room(s)
      </Typography>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <Box sx={{ display: "flex", justifyContent: "center", p: 3 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Location</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Capacity</TableCell>
              <TableCell>Status</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.code}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>
                  {[r.campusName, r.buildingName, r.floorName].filter(Boolean).join(" / ") || "—"}
                </TableCell>
                <TableCell>{ROOM_TYPE_LABELS[r.roomType] ?? r.roomType}</TableCell>
                <TableCell>{r.capacity}</TableCell>
                <TableCell>{ROOM_STATUS_LABELS[r.status] ?? r.status}</TableCell>
                {canManage && (
                  <TableCell align="right">
                    <Button size="small" onClick={() => openEdit(r)}>
                      Edit
                    </Button>
                    <Button size="small" color="error" onClick={() => void handleDelete(r.id)}>
                      Delete
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit room" : "Add room"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="room-form" onSubmit={save}>
            <Controller
              name="floorId"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="floor">Floor</InputLabel>
                  <Select labelId="floor" label="Floor" value={field.value || ""} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {floors.map((f) => (
                      <MenuItem key={f.id} value={f.id}>
                        {f.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller name="code" control={form.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
            <Controller name="name" control={form.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller
              name="roomType"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="room-type">Room type</InputLabel>
                  <Select labelId="room-type" label="Room type" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(ROOM_TYPE_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="status"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="room-status">Status</InputLabel>
                  <Select labelId="room-status" label="Status" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(ROOM_STATUS_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="capacity"
              control={form.control}
              render={({ field }) => (
                <TextField {...field} label="Capacity" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />
              )}
            />
            <FormGroup row>
              {FEATURE_OPTIONS.map((opt) => (
                <FormControlLabel
                  key={opt.flag}
                  control={<Checkbox checked={(featureFlags & opt.flag) === opt.flag} onChange={() => toggleFeature(opt.flag)} />}
                  label={opt.label}
                />
              ))}
            </FormGroup>
            <Controller
              name="isActive"
              control={form.control}
              render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="room-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default RoomsPage;
