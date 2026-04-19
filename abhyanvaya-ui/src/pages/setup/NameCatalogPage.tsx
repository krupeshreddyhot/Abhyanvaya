import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import api from "../../api/axios";

export type IdName = { id: number; name: string };

type Props = {
  title: string;
  /** API path segment after /api, e.g. "language" */
  resourcePath: string;
};

const errMsg = (e: unknown): string => {
  const ax = e as { response?: { data?: unknown } };
  const d = ax.response?.data;
  if (typeof d === "string") return d;
  return "Request failed.";
};

const NameCatalogPage = ({ title, resourcePath }: Props) => {
  const [rows, setRows] = useState<IdName[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editingId, setEditingId] = useState(0);
  const [name, setName] = useState("");

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.get<IdName[]>(`/${resourcePath}`);
      setRows(res.data);
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [resourcePath]);

  const openAdd = () => {
    setEditingId(0);
    setName("");
    setDialogOpen(true);
  };

  const openEdit = (r: IdName) => {
    setEditingId(r.id);
    setName(r.name);
    setDialogOpen(true);
  };

  const save = async () => {
    const n = name.trim();
    if (!n) {
      setError("Name is required.");
      return;
    }
    setSaving(true);
    setError(null);
    setMessage(null);
    try {
      if (editingId) {
        await api.put(`/${resourcePath}`, { id: editingId, name: n });
        setMessage("Updated successfully.");
      } else {
        await api.post(`/${resourcePath}`, { name: n });
        setMessage("Created successfully.");
      }
      setDialogOpen(false);
      await load();
    } catch (e) {
      setError(errMsg(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
        <Typography variant="h5">{title}</Typography>
        <Button variant="contained" onClick={openAdd}>
          Add
        </Button>
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
              <TableCell>Name</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r) => (
              <TableRow key={r.id} hover>
                <TableCell>{r.name}</TableCell>
                <TableCell align="right">
                  <Button size="small" onClick={() => openEdit(r)}>
                    Edit
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

      <Dialog open={dialogOpen} onClose={() => !saving && setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editingId ? "Edit" : "Add"}</DialogTitle>
        <DialogContent>
          <TextField
            label="Name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            fullWidth
            sx={{ mt: 1 }}
            autoFocus
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)} disabled={saving}>
            Cancel
          </Button>
          <Button variant="contained" onClick={() => void save()} disabled={saving}>
            {saving ? "Saving…" : "Save"}
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default NameCatalogPage;
