import { useEffect, useState } from "react";
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
import { Link as RouterLink, useNavigate, useSearchParams } from "react-router-dom";
import { resetPasswordWithToken } from "../services/authService";
import { useAuth } from "../context/AuthContext";

const ResetPasswordPage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { login } = useAuth();
  const [token, setToken] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [show, setShow] = useState({ t: false, n: false, v: false });

  useEffect(() => {
    const t = searchParams.get("token");
    if (t) setToken(t);
  }, [searchParams]);

  const submit = async () => {
    setError(null);
    if (!token.trim()) {
      setError("Reset token is required.");
      return;
    }
    if (next.length < 8) {
      setError("Password must be at least 8 characters.");
      return;
    }
    if (next !== confirm) {
      setError("Passwords do not match.");
      return;
    }
    setLoading(true);
    try {
      const res = await resetPasswordWithToken(token.trim(), next);
      login(res.data.token);
      navigate("/dashboard", { replace: true });
    } catch (e) {
      const d = (e as { response?: { data?: unknown } }).response?.data;
      setError(typeof d === "string" ? d : "Reset failed.");
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
            Reset password
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Paste the token from the forgot-password step and choose a new password.
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Stack spacing={2}>
            <TextField
              label="Reset token"
              fullWidth
              value={token}
              onChange={(e) => setToken(e.target.value)}
              multiline
              minRows={2}
              type={show.t ? "text" : "password"}
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton onClick={() => setShow((s) => ({ ...s, t: !s.t }))} edge="end" size="small">
                        {show.t ? <VisibilityOff /> : <Visibility />}
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
              label="Confirm password"
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
            <Button variant="contained" fullWidth disabled={loading} onClick={() => void submit()}>
              {loading ? <CircularProgress size={24} color="inherit" /> : "Set password & sign in"}
            </Button>
            <Button component={RouterLink} to="/" variant="text">
              Back to login
            </Button>
          </Stack>
        </Paper>
      </Container>
    </Box>
  );
};

export default ResetPasswordPage;
