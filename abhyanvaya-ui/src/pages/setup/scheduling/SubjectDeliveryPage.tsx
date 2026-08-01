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
import { listSubjectCatalog } from "../../../services/setupService";
import {
  createSubjectDeliveryType,
  deleteSubjectDeliveryType,
  listRoomFeatures,
  listSubjectDeliveryTypes,
  updateSubjectDeliveryFields,
  updateSubjectDeliveryType,
  type CreateSubjectDeliveryTypeRequest,
  type SubjectDeliveryTypeDto,
  type UpdateSubjectDeliveryFieldsRequest,
} from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";
import { ROOM_TYPE_LABELS } from "./schedulingEnumLabels";

type DeliveryTypeForm = CreateSubjectDeliveryTypeRequest;

const SubjectDeliveryPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingSubjectDeliveryManage);

  const [types, setTypes] = useState<SubjectDeliveryTypeDto[]>([]);
  const [subjects, setSubjects] = useState<{ id: number; label: string }[]>([]);
  const [roomFeatures, setRoomFeatures] = useState<{ id: number; label: string }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [typeDialogOpen, setTypeDialogOpen] = useState(false);
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [editingTypeId, setEditingTypeId] = useState(0);
  const [saving, setSaving] = useState(false);

  const typeForm = useForm<DeliveryTypeForm>({
    defaultValues: { code: "", name: "", sortOrder: 0, isActive: true },
  });

  const assignForm = useForm<UpdateSubjectDeliveryFieldsRequest>({
    defaultValues: {
      subjectId: 0,
      deliveryTypeId: 0,
      preferredRoomFeatureId: null,
      requiresAttendance: true,
      expectedCapacity: null,
      requiresRoomType: null,
    },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [t, subs, feats] = await Promise.all([
        listSubjectDeliveryTypes(),
        listSubjectCatalog(),
        listRoomFeatures({ isActive: true }),
      ]);
      setTypes(t.data);
      setSubjects(
        subs.data.map((s) => ({
          id: s.tenantSubjectId,
          label: `${s.code ?? ""} ${s.name} (${s.courseName})`.trim(),
        })),
      );
      setRoomFeatures(feats.data.map((f) => ({ id: f.id, label: `${f.code} — ${f.name}` })));
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openAddType = () => {
    setEditingTypeId(0);
    typeForm.reset({ code: "", name: "", sortOrder: types.length, isActive: true });
    setTypeDialogOpen(true);
  };

  const openEditType = (t: SubjectDeliveryTypeDto) => {
    setEditingTypeId(t.id);
    typeForm.reset({ code: t.code, name: t.name, sortOrder: t.sortOrder, isActive: t.isActive });
    setTypeDialogOpen(true);
  };

  const saveType = typeForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingTypeId) await updateSubjectDeliveryType(editingTypeId, { ...values, id: editingTypeId });
      else await createSubjectDeliveryType(values);
      setMessage(editingTypeId ? "Delivery type updated." : "Delivery type created.");
      setTypeDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDeleteType = async (id: number) => {
    if (!window.confirm("Delete this delivery type?")) return;
    try {
      await deleteSubjectDeliveryType(id);
      setMessage("Delivery type deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const openAssign = () => {
    assignForm.reset({
      subjectId: subjects[0]?.id ?? 0,
      deliveryTypeId: types[0]?.id ?? 0,
      preferredRoomFeatureId: null,
      requiresAttendance: true,
      expectedCapacity: null,
      requiresRoomType: null,
    });
    setAssignDialogOpen(true);
  };

  const saveAssign = assignForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      await updateSubjectDeliveryFields(values.subjectId, values);
      setMessage("Subject delivery fields updated.");
      setAssignDialogOpen(false);
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
          Subject delivery
        </Typography>
        {canManage && (
          <>
            <Button variant="outlined" onClick={openAssign}>
              Assign to subject
            </Button>
            <Button variant="contained" onClick={openAddType}>
              Add delivery type
            </Button>
          </>
        )}
      </Box>

      <Typography variant="body2" color="text.secondary">
        Delivery type catalog and per-subject delivery requirements (room type, capacity, attendance).
      </Typography>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Code</TableCell>
              <TableCell>Name</TableCell>
              <TableCell>Sort</TableCell>
              <TableCell>Active</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {types.map((t) => (
              <TableRow key={t.id} hover>
                <TableCell>{t.code}</TableCell>
                <TableCell>{t.name}</TableCell>
                <TableCell>{t.sortOrder}</TableCell>
                <TableCell>{t.isActive ? "Yes" : "No"}</TableCell>
                {canManage && (
                  <TableCell align="right">
                    <Button size="small" onClick={() => openEditType(t)}>
                      Edit
                    </Button>
                    <Button size="small" color="error" onClick={() => void handleDeleteType(t.id)}>
                      Delete
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={typeDialogOpen} onClose={() => !saving && setTypeDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingTypeId ? "Edit delivery type" : "Add delivery type"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="type-form" onSubmit={saveType}>
            <Controller name="code" control={typeForm.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
            <Controller name="name" control={typeForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller name="sortOrder" control={typeForm.control} render={({ field }) => <TextField {...field} label="Sort order" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="isActive" control={typeForm.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setTypeDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="type-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={assignDialogOpen} onClose={() => !saving && setAssignDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Assign delivery fields to subject</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="assign-form" onSubmit={saveAssign}>
            <Controller
              name="subjectId"
              control={assignForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="sub">Subject</InputLabel>
                  <Select labelId="sub" label="Subject" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {subjects.map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        {s.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="deliveryTypeId"
              control={assignForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="dt">Delivery type</InputLabel>
                  <Select labelId="dt" label="Delivery type" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {types.filter((t) => t.isActive).map((t) => (
                      <MenuItem key={t.id} value={t.id}>
                        {t.code} — {t.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="preferredRoomFeatureId"
              control={assignForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="rf">Preferred room feature</InputLabel>
                  <Select
                    labelId="rf"
                    label="Preferred room feature"
                    value={field.value === null ? "" : field.value}
                    onChange={(e) => {
                      const v = e.target.value as number | string;
                      field.onChange(v === "" ? null : Number(v));
                    }}
                  >
                    <MenuItem value="">None</MenuItem>
                    {roomFeatures.map((f) => (
                      <MenuItem key={f.id} value={f.id}>
                        {f.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              name="requiresRoomType"
              control={assignForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="rt">Requires room type</InputLabel>
                  <Select
                    labelId="rt"
                    label="Requires room type"
                    value={field.value === null ? "" : field.value}
                    onChange={(e) => {
                      const v = e.target.value as number | string;
                      field.onChange(v === "" ? null : Number(v));
                    }}
                  >
                    <MenuItem value="">None</MenuItem>
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
              name="expectedCapacity"
              control={assignForm.control}
              render={({ field }) => (
                <TextField
                  label="Expected capacity"
                  type="number"
                  fullWidth
                  value={field.value ?? ""}
                  onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}
                />
              )}
            />
            <Controller
              name="requiresAttendance"
              control={assignForm.control}
              render={({ field }) => (
                <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Requires attendance" />
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAssignDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="assign-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default SubjectDeliveryPage;
