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
  FormControlLabel,
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
  createHolidayType,
  deleteHolidayType,
  listHolidayTypes,
  updateHolidayType,
  type CreateHolidayTypeCatalogRequest,
  type HolidayTypeCatalogDto,
} from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";

type HolidayTypeForm = CreateHolidayTypeCatalogRequest;

const HolidayTypesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingHolidayTypesManage);

  const [rows, setRows] = useState<HolidayTypeCatalogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<HolidayTypeForm>({
    defaultValues: { code: "", name: "", colour: "#1976d2", priority: 0, sortOrder: 0, isActive: true },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listHolidayTypes();
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openAdd = () => {
    setEditingId(0);
    form.reset({ code: "", name: "", colour: "#1976d2", priority: 0, sortOrder: rows.length, isActive: true });
    setDialogOpen(true);
  };

  const openEdit = (r: HolidayTypeCatalogDto) => {
    setEditingId(r.id);
    form.reset({ code: r.code, name: r.name, colour: r.colour, priority: r.priority, sortOrder: r.sortOrder, isActive: r.isActive });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) await updateHolidayType(editingId, { ...values, id: editingId });
      else await createHolidayType(values);
      setMessage(editingId ? "Holiday type updated." : "Holiday type created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this holiday type?")) return;
    try {
      await deleteHolidayType(id);
      setMessage("Holiday type deleted.");
      await load();
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
          Holiday types
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add holiday type
          </Button>
        )}
      </Box>

      <Typography variant="body2" color="text.secondary">
        Catalog of holiday types with colour and priority used when creating holidays.
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
              <TableCell>Colour</TableCell>
              <TableCell>Priority</TableCell>
              <TableCell>Sort</TableCell>
              <TableCell>Active</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.code}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    <Box sx={{ width: 20, height: 20, borderRadius: 0.5, bgcolor: r.colour, border: "1px solid #ccc" }} />
                    {r.colour}
                  </Box>
                </TableCell>
                <TableCell>{r.priority}</TableCell>
                <TableCell>{r.sortOrder}</TableCell>
                <TableCell>{r.isActive ? "Yes" : "No"}</TableCell>
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
        <DialogTitle>{editingId ? "Edit holiday type" : "Add holiday type"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="ht-form" onSubmit={save}>
            <Controller name="code" control={form.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
            <Controller name="name" control={form.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller
              name="colour"
              control={form.control}
              render={({ field }) => (
                <TextField {...field} label="Colour" type="color" fullWidth slotProps={{ inputLabel: { shrink: true } }} />
              )}
            />
            <Controller name="priority" control={form.control} render={({ field }) => <TextField {...field} label="Priority" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="sortOrder" control={form.control} render={({ field }) => <TextField {...field} label="Sort order" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="isActive" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="ht-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default HolidayTypesPage;
