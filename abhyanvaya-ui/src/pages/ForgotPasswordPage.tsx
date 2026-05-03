import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Container,
  MenuItem,
  Paper,
  Stack,
  TextField,
  Typography,
  CircularProgress,
} from "@mui/material";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { forgotPassword, getUniversities, type UniversityOption } from "../services/authService";

const ForgotPasswordPage = () => {
  const navigate = useNavigate();
  const [universities, setUniversities] = useState<UniversityOption[]>([]);
  const [universityCode, setUniversityCode] = useState("");
  const [collegeCode, setCollegeCode] = useState("");
  const [username, setUsername] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [resetToken, setResetToken] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void (async () => {
      try {
        const res = await getUniversities();
        setUniversities(res.data);
        if (res.data.length > 0) setUniversityCode(res.data[0].code);
      } catch {
        setError("Could not load universities.");
      }
    })();
  }, []);

  const submit = async () => {
    setLoading(true);
    setError(null);
    setMessage(null);
    setResetToken(null);
    try {
      const res = await forgotPassword(universityCode, collegeCode, username);
      setResetToken(res.data.resetToken ?? null);
      setMessage(res.data.message ?? null);
    } catch (e) {
      const d = (e as { response?: { data?: unknown } }).response?.data;
      setError(typeof d === "string" ? d : "Request failed.");
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
            Forgot password
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Enter your institution codes and username. If the account exists, you will receive a one-time reset token
            (valid 1 hour). Then open <strong>Reset password</strong> and paste the token.
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          {message && (
            <Alert severity="info" sx={{ mb: 2 }}>
              {message}
            </Alert>
          )}
          {resetToken && (
            <Alert severity="success" sx={{ mb: 2 }}>
              <Typography variant="body2" sx={{ wordBreak: "break-all", fontFamily: "monospace" }}>
                {resetToken}
              </Typography>
              <Button size="small" sx={{ mt: 1 }} variant="outlined" onClick={() => navigate(`/reset-password?token=${encodeURIComponent(resetToken)}`)}>
                Continue to reset password
              </Button>
            </Alert>
          )}

          <Stack spacing={2}>
            <TextField
              select
              label="University"
              fullWidth
              value={universityCode}
              onChange={(e) => setUniversityCode(e.target.value)}
              disabled={universities.length === 0}
            >
              {universities.map((u) => (
                <MenuItem key={u.code} value={u.code}>
                  {u.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="College code"
              fullWidth
              value={collegeCode}
              onChange={(e) => setCollegeCode(e.target.value.toUpperCase())}
            />
            <TextField label="Username" fullWidth value={username} onChange={(e) => setUsername(e.target.value)} />
            <Button variant="contained" fullWidth disabled={loading} onClick={() => void submit()}>
              {loading ? <CircularProgress size={24} color="inherit" /> : "Request reset token"}
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

export default ForgotPasswordPage;
