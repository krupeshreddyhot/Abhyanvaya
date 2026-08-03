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
  createSubjectCategory,
  deleteSubjectCategory,
  listSubjectCategories,
  updateSubjectCategory,
  updateSubjectSchedulingCategory,
  type CreateSubjectCategoryRequest,
  type SubjectCategoryDto,
  type UpdateSubjectSchedulingCategoryRequest,
} from "../../../services/schedulingService";
import { errMsg } from "./schedulingFormUtils";
import { ROOM_TYPE_LABELS } from "./schedulingEnumLabels";

type CategoryForm = CreateSubjectCategoryRequest;

const SubjectCategoriesPage = () => {
  const { hasPermission } = useAuth();
  const canManage = hasPermission(PermissionKeys.SchedulingManage);

  const [categories, setCategories] = useState<SubjectCategoryDto[]>([]);
  const [subjects, setSubjects] = useState<{ id: number; label: string }[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [catDialogOpen, setCatDialogOpen] = useState(false);
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [editingCatId, setEditingCatId] = useState(0);
  const [saving, setSaving] = useState(false);

  const catForm = useForm<CategoryForm>({
    defaultValues: { code: "", name: "", sortOrder: 0, isActive: true },
  });

  const assignForm = useForm<UpdateSubjectSchedulingCategoryRequest>({
    defaultValues: {
      subjectId: 0,
      subjectCategoryId: 0,
      requiresRoomType: null,
      defaultDurationMinutes: 60,
      requiresLabEquipment: false,
    },
  });

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [cats, subs] = await Promise.all([listSubjectCategories(), listSubjectCatalog()]);
      setCategories(cats.data);
      setSubjects(
        subs.data.map((s) => ({
          id: s.tenantSubjectId,
          label: `${s.code ?? ""} ${s.name} (${s.courseName})`.trim(),
        })),
      );
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const openAddCategory = () => {
    setEditingCatId(0);
    catForm.reset({ code: "", name: "", sortOrder: categories.length, isActive: true });
    setCatDialogOpen(true);
  };

  const openEditCategory = (c: SubjectCategoryDto) => {
    setEditingCatId(c.id);
    catForm.reset({ code: c.code, name: c.name, sortOrder: c.sortOrder, isActive: c.isActive });
    setCatDialogOpen(true);
  };

  const saveCategory = catForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingCatId) await updateSubjectCategory(editingCatId, { ...values, id: editingCatId });
      else await createSubjectCategory(values);
      setMessage(editingCatId ? "Category updated." : "Category created.");
      setCatDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  });

  const handleDeleteCategory = async (id: number) => {
    if (!window.confirm("Delete this category?")) return;
    try {
      await deleteSubjectCategory(id);
      setMessage("Category deleted.");
      await load();
    } catch (e) {
      setError(errMsg(e));
    }
  };

  const openAssign = () => {
    assignForm.reset({
      subjectId: subjects[0]?.id ?? 0,
      subjectCategoryId: categories[0]?.id ?? 0,
      requiresRoomType: null,
      defaultDurationMinutes: 60,
      requiresLabEquipment: false,
    });
    setAssignDialogOpen(true);
  };

  const saveAssign = assignForm.handleSubmit(async (values) => {
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      await updateSubjectSchedulingCategory(values.subjectId, values);
      setMessage("Subject category fields updated.");
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
          Subject categories
        </Typography>
        {canManage && (
          <>
            <Button variant="outlined" onClick={openAssign}>
              Assign to subject
            </Button>
            <Button variant="contained" onClick={openAddCategory}>
              Add category
            </Button>
          </>
        )}
      </Box>

      <Typography variant="body2" color="text.secondary">
        Categories are seeded when the list loads if none exist. Assign category, room type, duration, and lab requirements to individual subjects.
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
            {categories.map((c) => (
              <TableRow key={c.id} hover>
                <TableCell>{c.code}</TableCell>
                <TableCell>{c.name}</TableCell>
                <TableCell>{c.sortOrder}</TableCell>
                <TableCell>{c.isActive ? "Yes" : "No"}</TableCell>
                {canManage && (
                  <TableCell align="right">
                    <Button size="small" onClick={() => openEditCategory(c)}>
                      Edit
                    </Button>
                    <Button size="small" color="error" onClick={() => void handleDeleteCategory(c.id)}>
                      Delete
                    </Button>
                  </TableCell>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={catDialogOpen} onClose={() => !saving && setCatDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingCatId ? "Edit category" : "Add category"}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }} component="form" id="cat-form" onSubmit={saveCategory}>
            <Controller name="code" control={catForm.control} render={({ field }) => <TextField {...field} label="Code" fullWidth required />} />
            <Controller name="name" control={catForm.control} render={({ field }) => <TextField {...field} label="Name" fullWidth required />} />
            <Controller name="sortOrder" control={catForm.control} render={({ field }) => <TextField {...field} label="Sort order" type="number" fullWidth onChange={(e) => field.onChange(Number(e.target.value))} />} />
            <Controller name="isActive" control={catForm.control} render={({ field }) => <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Active" />} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCatDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" form="cat-form" variant="contained" disabled={saving}>
            Save
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={assignDialogOpen} onClose={() => !saving && setAssignDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Assign category to subject</DialogTitle>
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
              name="subjectCategoryId"
              control={assignForm.control}
              render={({ field }) => (
                <FormControl fullWidth>
                  <InputLabel id="cat">Category</InputLabel>
                  <Select labelId="cat" label="Category" value={field.value} onChange={(e) => field.onChange(Number(e.target.value))}>
                    {categories.map((c) => (
                      <MenuItem key={c.id} value={c.id}>
                        {c.code} — {c.name}
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
              name="defaultDurationMinutes"
              control={assignForm.control}
              render={({ field }) => (
                <TextField
                  {...field}
                  value={field.value ?? ""}
                  label="Default duration (minutes)"
                  type="number"
                  fullWidth
                  onChange={(e) => field.onChange(e.target.value ? Number(e.target.value) : null)}
                />
              )}
            />
            <Controller
              name="requiresLabEquipment"
              control={assignForm.control}
              render={({ field }) => (
                <FormControlLabel control={<Checkbox checked={field.value} onChange={(_, v) => field.onChange(v)} />} label="Requires lab equipment" />
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

export default SubjectCategoriesPage;
