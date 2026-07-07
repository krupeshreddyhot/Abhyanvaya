import {
  Avatar,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  List,
  ListItemButton,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { getStudents, type StudentRecordDto } from "../../services/studentsService";

type AssignStudentDialogProps = {
  open: boolean;
  onClose: () => void;
  onAssign: (studentId: number) => Promise<void>;
};

export function AssignStudentDialog({ open, onClose, onAssign }: AssignStudentDialogProps) {
  const [search, setSearch] = useState("");
  const [students, setStudents] = useState<StudentRecordDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [assigningId, setAssigningId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) {
      setSearch("");
      setStudents([]);
      setError(null);
      return;
    }

    const timer = setTimeout(async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await getStudents({
          search: search.trim() || undefined,
          pageNumber: 1,
          pageSize: 20,
        });
        setStudents(response.data.data);
      } catch {
        setError("Failed to search students.");
        setStudents([]);
      } finally {
        setLoading(false);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [open, search]);

  const handleAssign = async (studentId: number) => {
    setAssigningId(studentId);
    setError(null);
    try {
      await onAssign(studentId);
      onClose();
    } catch {
      setError("Failed to assign student.");
    } finally {
      setAssigningId(null);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Assign student</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 1 }}>
          <TextField
            label="Search by name or number"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            fullWidth
            autoFocus
          />
          {error && (
            <Typography variant="body2" color="error">
              {error}
            </Typography>
          )}
          {loading ? (
            <Box sx={{ display: "flex", justifyContent: "center", py: 3 }}>
              <CircularProgress size={28} />
            </Box>
          ) : (
            <List dense disablePadding>
              {students.map((student) => (
                <ListItemButton
                  key={student.id}
                  disabled={assigningId != null}
                  onClick={() => void handleAssign(student.id)}
                >
                  <Avatar sx={{ width: 36, height: 36, mr: 1.5 }}>
                    {student.name.charAt(0)}
                  </Avatar>
                  <ListItemText
                    primary={student.name}
                    secondary={`${student.studentNumber} · ${student.courseName} / ${student.groupName}`}
                  />
                  {assigningId === student.id && <CircularProgress size={18} sx={{ ml: 1 }} />}
                </ListItemButton>
              ))}
              {!students.length && (
                <Typography variant="body2" color="text.secondary" sx={{ py: 2 }}>
                  No students found.
                </Typography>
              )}
            </List>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
      </DialogActions>
    </Dialog>
  );
}
