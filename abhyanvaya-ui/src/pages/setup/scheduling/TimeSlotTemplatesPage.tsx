import { useCallback, useEffect, useState } from "react";
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
import VisibilityIcon from "@mui/icons-material/Visibility";
import StarIcon from "@mui/icons-material/Star";
import { Controller, useForm } from "react-hook-form";
import { PermissionKeys } from "../../../auth/permissionKeys";
import { useAuth } from "../../../context/AuthContext";
import {
  cloneTimeSlotTemplate,
  createTimeSlotTemplate,
  deleteTimeSlotTemplate,
  listTimeSlotTemplates,
  previewTimeSlotTemplate,
  setDefaultTimeSlotTemplate,
  updateTimeSlotTemplate,
  type CloneTimeSlotTemplateRequest,
  type CreateTimeSlotTemplateRequest,
  type TimeSlotTemplateDto,
  type TimeSlotTemplatePreviewDto,
} from "../../../services/schedulingService";
import { errMsg, formatTimeSpan } from "./schedulingFormUtils";
import { TEMPLATE_TYPE_LABELS } from "./schedulingEnumLabels";

type TemplateForm = CreateTimeSlotTemplateRequest;

const TimeSlotTemplatesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingTemplateManage);

  const [rows, setRows] = useState<TimeSlotTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [cloneOpen, setCloneOpen] = useState(false);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [preview, setPreview] = useState<TimeSlotTemplatePreviewDto | null>(null);
  const [editingId, setEditingId] = useState(0);
  const [cloneSourceId, setCloneSourceId] = useState(0);
  const [saving, setSaving] = useState(false);

  const form = useForm<TemplateForm>({
    defaultValues: { name: "", description: "", templateType: 1, isDefault: false },
  });

  const cloneForm = useForm<CloneTimeSlotTemplateRequest>({
    defaultValues: { sourceTemplateId: 0, name: "", description: "", templateType: 1, isDefault: false },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await listTimeSlotTemplates();
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
    form.reset({ name: "", description: "", templateType: 1, isDefault: false });
    setDialogOpen(true);
  };

  const openEdit = (r: TimeSlotTemplateDto) => {
    setEditingId(r.id);
    form.reset({
      name: r.name,
      description: r.description ?? "",
      templateType: r.templateType,
      isDefault: r.isDefault,
    });
    setDialogOpen(true);
  };

  const save = form.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      const payload = { ...values, description: values.description?.trim() || null };
      if (editingId) await updateTimeSlotTemplate(editingId, { ...payload, id: editingId });
      else await createTimeSlotTemplate(payload);
      setMessage(editingId ? "Template updated." : "Template created.");
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDelete = async (id: number) => {
    if (!window.confirm("Delete this template?")) return;
    try {
      await deleteTimeSlotTemplate(id);
      setMessage("Template deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const openClone = (r: TimeSlotTemplateDto) => {
    setCloneSourceId(r.id);
    cloneForm.reset({
      sourceTemplateId: r.id,
      name: `${r.name} (copy)`,
      description: r.description ?? "",
      templateType: r.templateType,
      isDefault: false,
    });
    setCloneOpen(true);
  };

  const saveClone = cloneForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    try {
      await cloneTimeSlotTemplate({ ...values, description: values.description?.trim() || null });
      setMessage("Template cloned.");
      setCloneOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleSetDefault = async (id: number) => {
    try {
      await setDefaultTimeSlotTemplate(id);
      setMessage("Default template updated.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const openPreview = async (id: number) => {
    setError(null);
    try {
      const res = await previewTimeSlotTemplate(id);
      setPreview(res.data);
      setPreviewOpen(true);
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
          Time slot templates
        </Typography>
        {canManage && (
          <Button variant="contained" onClick={openAdd}>
            Add template
          </Button>
        )}
      </Box>

      {message && <Alert severity="success">{message}</Alert>}
      {error && <Alert severity="error">{error}</Alert>}

      {loading ? (
        <CircularProgress />
      ) : (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Type</TableCell>
              <TableCell>Sets</TableCell>
              <TableCell>Slots</TableCell>
              <TableCell>Default</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell>{TEMPLATE_TYPE_LABELS[r.templateType] ?? r.templateType}</TableCell>
                <TableCell>{r.setCount}</TableCell>
                <TableCell>{r.slotCount}</TableCell>
                <TableCell>{r.isDefault ? <Chip size="small" color="primary" label="Default" /> : "—"}</TableCell>
                <TableCell align="right">
                  <Button size="small" startIcon={<VisibilityIcon />} onClick={() => void openPreview(r.id)}>
                    Preview
                  </Button>
                  {canManage && (
                    <>
                      <Button size="small" onClick={() => openEdit(r)}>
                        Edit
                      </Button>
                      <Button size="small" startIcon={<ContentCopyIcon />} onClick={() => openClone(r)}>
                        Clone
                      </Button>
                      {!r.isDefault && (
                        <Button size="small" startIcon={<StarIcon />} onClick={() => void handleSetDefault(r.id)}>
                          Set default
                        </Button>
                      )}
                      <Button size="small" color="error" onClick={() => void handleDelete(r.id)}>
                        Delete
                      </Button>
                    </>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit template" : "Add template"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="tpl-form" onSubmit={save}>
            <Controller name="name" control={form.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller name="description" control={form.control} render={({ field }) => <TextField {...field} label="Description" fullWidth multiline minRows={2} />} />
            <Controller
              name="templateType"
              control={form.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="tt">Template type</InputLabel>
                  <Select labelId="tt" label="Template type" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(TEMPLATE_TYPE_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller name="isDefault" control={form.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Default" />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="tpl-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={cloneOpen} onClose={() => !saving && setCloneOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Clone template</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="clone-form" onSubmit={saveClone}>
            <Typography variant="body2" color="text.secondary">
              Source template ID: {cloneSourceId}
            </Typography>
            <Controller name="name" control={cloneForm.control} render={({ field }) => <TextField {...field} label="New name" fullWidth required />} />
            <Controller name="description" control={cloneForm.control} render={({ field }) => <TextField {...field} label="Description" fullWidth />} />
            <Controller
              name="templateType"
              control={cloneForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="ctt">Template type</InputLabel>
                  <Select labelId="ctt" label="Template type" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {Object.entries(TEMPLATE_TYPE_LABELS).map(([k, v]) => (
                      <MenuItem key={k} value={Number(k)}>
                        {v}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller name="isDefault" control={cloneForm.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Set as default" />} />
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

      <Dialog open={previewOpen} onClose={() => setPreviewOpen(false)} fullWidth maxWidth="md">
        <DialogTitle>{preview?.name ?? "Template preview"}</DialogTitle>
        <DialogContent>
          {preview && (
            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary">
                {TEMPLATE_TYPE_LABELS[preview.templateType]} · {preview.sets.length} set(s) · {preview.slots.length} slot(s)
              </Typography>
              {preview.sets.map((set) => (
                <Box key={set.id}>
                  <Typography variant="subtitle2">
                    {set.name} ({set.code})
                  </Typography>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>Period</TableCell>
                        <TableCell>Name</TableCell>
                        <TableCell>Start</TableCell>
                        <TableCell>End</TableCell>
                        <TableCell>Duration</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {preview.slots
                        .filter((s) => s.timeSlotSetId === set.id)
                        .map((s) => (
                          <TableRow key={s.id}>
                            <TableCell>{s.periodNumber ?? "—"}</TableCell>
                            <TableCell>{s.name}</TableCell>
                            <TableCell>{formatTimeSpan(s.startTime)}</TableCell>
                            <TableCell>{formatTimeSpan(s.endTime)}</TableCell>
                            <TableCell>{s.durationMinutes} min</TableCell>
                          </TableRow>
                        ))}
                    </TableBody>
                  </Table>
                </Box>
              ))}
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPreviewOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default TimeSlotTemplatesPage;
