import { useCallback, useEffect, useState } from "react";
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
  FormControlLabel,
  Stack,
  Switch,
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
import { useForm, Controller } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  cloneAcademicYear,
  createAcademicYear,
  deleteAcademicYear,
  listAcademicYears,
  setCurrentAcademicYear,
  updateAcademicYear,
  type AcademicYearDto,
  type ClonePreviousYearRequest,
  type CreateAcademicYearRequest,
} from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";

type YearForm = CreateAcademicYearRequest;

type CloneForm = ClonePreviousYearRequest;

const defaultYearForm = (): YearForm => ({
  name: "",
  code: "",
  startDate: "",
  endDate: "",
  isCurrent: false,
});

const AcademicYearsPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [rows, setRows] = useState<AcademicYearDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [cloneOpen, setCloneOpen] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [saving, setSaving] = useState(false);

  const yearForm = useForm<YearForm>({ defaultValues: defaultYearForm() });
  const cloneForm = useForm<CloneForm>({
    defaultValues: { sourceYearId: 0, name: "", code: "", startDate: "", endDate: "", setAsCurrent: false },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listAcademicYears();
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
    yearForm.reset(defaultYearForm());
    setDialogOpen(true);
  };

  const openEdit = (r: AcademicYearDto) => {
    setEditingId(r.id);
    yearForm.reset({
      name: r.name,
      code: r.code,
      startDate: r.startDate,
      endDate: r.endDate,
      isCurrent: r.isCurrent,
    });
    setDialogOpen(true);
  };

  const openClone = (sourceYearId: number) => {
    cloneForm.reset({
      sourceYearId,
      name: "",
      code: "",
      startDate: "",
      endDate: "",
      setAsCurrent: false,
    });
    setCloneOpen(true);
  };

  const saveYear = yearForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) {
        await updateAcademicYear(editingId, { ...values, id: editingId });
        setMessage("Academic year updated.");
      } else {
        await createAcademicYear(values);
        setMessage("Academic year created.");
      }
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const saveClone = cloneForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      await cloneAcademicYear(values);
      setMessage("Academic year cloned.");
      setCloneOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleSetCurrent = async (id: number) => {
    setError(null);
    setMessage(null);
    try {
      await setCurrentAcademicYear(id);
      setMessage("Current academic year updated.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this academic year?")) return;
    setError(null);
    try {
      await deleteAcademicYear(id);
      setMessage("Academic year deleted.");
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
          Academic years
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add year
          </Button>
        )}
      </Box>

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
              <TableCell>Start</TableCell>
              <TableCell>End</TableCell>
              <TableCell>Current</TableCell>
              {canManage && <TableCell align="right">Actions</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.code}</TableCell>
                <TableCell>{r.name}</TableCell>
                <TableCell>{r.startDate}</TableCell>
                <TableCell>{r.endDate}</TableCell>
                <TableCell>{r.isCurrent ? <Chip size="small" color="primary" label="Current" /> : "—"}</TableCell>
                {canManage && (
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end", flexWrap: "wrap" }}>
                      <Button size="small" onClick={() => openEdit(r)}>
                        Edit
                      </Button>
                      {!r.isCurrent && (
                        <Button size="small" onClick={() => void handleSetCurrent(r.id)}>
                          Set current
                        </Button>
                      )}
                      <Button size="small" onClick={() => openClone(r.id)}>
                        Clone
                      </Button>
                      <Button size="small" color="error" onClick={() => void handleDelete(r.id)}>
                        Delete
                      </Button>
                    </Stack>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit academic year" : "Add academic year"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="year-form" onSubmit={saveYear}>
            <Controller
              name="code"
              control={yearForm.control}
              rules={{ required: true }}
              render={({ field }) => (
                <TextField {...field} label="Code" fullWidth required onChange={(e) => field.onChange(e.target.value.toUpperCase())} />
              )}
            />
            <Controller
              name="name"
              control={yearForm.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="Name" fullWidth required />}
            />
            <Controller
              name="startDate"
              control={yearForm.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="Start date" type="date" fullWidth required slotProps={{ inputLabel: { shrink: true } }} />}
            />
            <Controller
              name="endDate"
              control={yearForm.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="End date" type="date" fullWidth required slotProps={{ inputLabel: { shrink: true } }} />}
            />
            <Controller
              name="isCurrent"
              control={yearForm.control}
              render={({ field }) => (
                <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Set as current" />
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="year-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={cloneOpen} onClose={() => !saving && setCloneOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Clone previous year</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="clone-form" onSubmit={saveClone}>
            <Controller
              name="code"
              control={cloneForm.control}
              rules={{ required: true }}
              render={({ field }) => (
                <TextField {...field} label="New code" fullWidth required onChange={(e) => field.onChange(e.target.value.toUpperCase())} />
              )}
            />
            <Controller
              name="name"
              control={cloneForm.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="New name" fullWidth required />}
            />
            <Controller
              name="startDate"
              control={cloneForm.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="Start date" type="date" fullWidth required slotProps={{ inputLabel: { shrink: true } }} />}
            />
            <Controller
              name="endDate"
              control={cloneForm.control}
              rules={{ required: true }}
              render={({ field }) => <TextField {...field} label="End date" type="date" fullWidth required slotProps={{ inputLabel: { shrink: true } }} />}
            />
            <Controller
              name="setAsCurrent"
              control={cloneForm.control}
              render={({ field }) => (
                <FormControlLabel control={<Switch checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Set as current" />
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCloneOpen(false)} disabled={saving}>
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

export default AcademicYearsPage;
