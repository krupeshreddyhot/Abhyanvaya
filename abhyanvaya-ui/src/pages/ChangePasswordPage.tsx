import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Container,
  Paper,
  Stack,
  TextField,
  Typography,
  CircularProgress,
  InputAdornment,
  IconButton,
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { useNavigate, useSearchParams } from "react-router-dom";
import { changePassword } from "../services/authService";
import { useAuth } from "../context/AuthContext";

const ChangePasswordPage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const isFirst = searchParams.get("first") === "1";
  const { login } = useAuth();

  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [show, setShow] = useState({ c: false, n: false, v: false });

  const submit = async () => {
    setError(null);
    if (next.length < 8) {
      setError("New password must be at least 8 characters.");
      return;
    }
    if (next !== confirm) {
      setError("New password and confirmation do not match.");
      return;
    }
    setLoading(true);
    try {
      const res = await changePassword(current, next);
      login(res.data.token);
      navigate("/dashboard", { replace: true });
    } catch (e) {
      const d = (e as { response?: { data?: unknown } }).response?.data;
      setError(typeof d === "string" ? d : "Could not update password.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "linear-gradient(135deg, #1e3c72, #2a5298)",
        p: 2,
      }}
    >
      <Container maxWidth="sm">
        <Paper elevation={6} sx={{ p: 4, borderRadius: 3 }}>
          <Typography variant="h5" gutterBottom>
            {isFirst ? "Set a new password" : "Change password"}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {isFirst
              ? "Your administrator provided a temporary password. Choose a new password to continue."
              : "Enter your current password and choose a new one."}
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Stack spacing={2}>
            <TextField
              label="Current password"
              type={show.c ? "text" : "password"}
              fullWidth
              value={current}
              onChange={(e) => setCurrent(e.target.value)}
              autoComplete="current-password"
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton onClick={() => setShow((s) => ({ ...s, c: !s.c }))} edge="end" size="small">
                        {show.c ? <VisibilityOff /> : <Visibility />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />
            <TextField
              label="New password"
              type={show.n ? "text" : "password"}
              fullWidth
              value={next}
              onChange={(e) => setNext(e.target.value)}
              helperText="At least 8 characters."
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton onClick={() => setShow((s) => ({ ...s, n: !s.n }))} edge="end" size="small">
                        {show.n ? <VisibilityOff /> : <Visibility />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />
            <TextField
              label="Confirm new password"
              type={show.v ? "text" : "password"}
              fullWidth
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton onClick={() => setShow((s) => ({ ...s, v: !s.v }))} edge="end" size="small">
                        {show.v ? <VisibilityOff /> : <Visibility />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />
            <Button variant="contained" size="large" fullWidth disabled={loading} onClick={() => void submit()}>
              {loading ? <CircularProgress size={24} color="inherit" /> : "Update password"}
            </Button>
            {!isFirst && (
              <Button variant="text" onClick={() => navigate(-1)}>
                Cancel
              </Button>
            )}
          </Stack>
        </Paper>
      </Container>
    </Box>
  );
};

export default ChangePasswordPage;
