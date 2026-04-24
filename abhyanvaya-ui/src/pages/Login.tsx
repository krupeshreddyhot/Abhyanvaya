import { useState } from "react";
import axios from "axios";
import {
  Container,
  TextField,
  Button,
  Typography,
  Box,
  Paper,
  CircularProgress,
  MenuItem,
  ToggleButton,
  ToggleButtonGroup,
  InputAdornment,
  IconButton,
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { getUniversities, login as loginApi, superAdminLogin, type UniversityOption } from "../services/authService";
import { useAuth } from "../context/AuthContext";
import { useNavigate } from "react-router-dom";
import { useEffect } from "react";

type LoginMode = "institution" | "superadmin";

function resolveLoginError(err: unknown, loginMode: LoginMode): string {
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7063/api";
  if (axios.isAxiosError(err) && (!err.response || err.code === "ERR_NETWORK")) {
    return loginMode === "superadmin"
      ? `Cannot reach ${apiBase}. On Cloudflare Pages set VITE_API_BASE_URL to your live API (…/api), redeploy, and add this site URL to Cors__ReactOrigin on the API.`
      : `Cannot reach ${apiBase}. Set VITE_API_BASE_URL and API CORS (see Super Admin guidance).`;
  }
  if (axios.isAxiosError(err) && err.response?.status === 401) {
    return loginMode === "superadmin"
      ? "Invalid credentials, or this UI is not talking to the Neon-backed API: set VITE_API_BASE_URL (Pages) and ConnectionStrings__DefaultConnection (API host) to Neon."
      : "Invalid university, college code, username or password.";
  }
  if (axios.isAxiosError(err) && err.response) {
    return `Request failed (${err.response.status}). Check API logs.`;
  }
  return loginMode === "superadmin"
    ? "Invalid Super Admin username or password."
    : "Invalid university, college code, username or password.";
}

/** Institution tab calls GET /auth/universities on load — same connectivity needs as login. */
function resolveUniversitiesLoadError(err: unknown): string {
  const apiBase = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7063/api";
  if (axios.isAxiosError(err) && (!err.response || err.code === "ERR_NETWORK")) {
    return `Cannot reach ${apiBase}. Set VITE_API_BASE_URL in your UI build (e.g. Cloudflare Pages) to your live API ending in /api, redeploy, and add this site's origin to Cors__ReactOrigin on the API.`;
  }
  if (axios.isAxiosError(err) && err.response) {
    const st = err.response.status;
    if (st === 403)
      return "Request blocked — add this page's URL to Cors__ReactOrigin on the API host.";
    return `Could not load universities (HTTP ${st}). Check API logs and database connection.`;
  }
  return `Unable to load universities. Using API base: ${apiBase}`;
}

const Login = () => {
  const [loginMode, setLoginMode] = useState<LoginMode>("institution");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [universityCode, setUniversityCode] = useState("");
  const [collegeCode, setCollegeCode] = useState("");
  const [universities, setUniversities] = useState<UniversityOption[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const navigate = useNavigate();
  const { login } = useAuth();

  useEffect(() => {
    setError("");
    if (loginMode !== "institution") return;

    const loadUniversities = async () => {
      try {
        const res = await getUniversities();
        setUniversities(res.data);
        if (res.data.length > 0) {
          setUniversityCode(res.data[0].code);
        } else {
          setUniversityCode("");
        }
      } catch (err) {
        setError(resolveUniversitiesLoadError(err));
      }
    };

    void loadUniversities();
  }, [loginMode]);

  const handleLogin = async () => {
    if (loading) return;

    setLoading(true);
    setError("");

    try {
      if (loginMode === "superadmin") {
        const res = await superAdminLogin(username, password);
        login(res.data.token);
        navigate("/dashboard");
        return;
      }

      if (universities.length === 0) {
        setError("No universities are registered yet. A Super Admin must sign in and create universities under Organization.");
        return;
      }

      const res = await loginApi(universityCode, collegeCode, username, password);
      login(res.data.token);
      navigate("/dashboard");
    } catch (err) {
      setError(resolveLoginError(err, loginMode));
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        height: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "linear-gradient(135deg, #1e3c72, #2a5298)",
      }}
    >
      <Container maxWidth="sm">
        <Paper
          elevation={6}
          sx={{
            padding: 4,
            borderRadius: 3,
            textAlign: "center",
          }}
        >
          <Typography variant="h4" gutterBottom sx={{ fontWeight: "bold" }}>
            Abhyanvaya
          </Typography>

          <Typography variant="subtitle1" color="text.secondary" sx={{ mb: 2 }}>
              Institute Management System
          </Typography>

          <Box sx={{ display: "flex", justifyContent: "center", mb: 2 }}>
            <ToggleButtonGroup
              exclusive
              size="small"
              value={loginMode}
              onChange={(_, v) => v && setLoginMode(v)}
              color="primary"
            >
              <ToggleButton value="institution">Institution</ToggleButton>
              <ToggleButton value="superadmin">Super Admin</ToggleButton>
            </ToggleButtonGroup>
          </Box>

          <Box
            sx={{
              display: "flex",
              flexDirection: "column",
              gap: 2,
            }}
          >
            {loginMode === "institution" && (
              <>
                <TextField
                  select
                  label="University"
                  variant="outlined"
                  size="medium"
                  margin="normal"
                  fullWidth
                  value={universityCode}
                  onChange={(e) => setUniversityCode(e.target.value)}
                  disabled={universities.length === 0}
                  helperText={
                    universities.length === 0
                      ? "No universities yet — use Super Admin to add organizations."
                      : undefined
                  }
                >
                  {universities.map((u) => (
                    <MenuItem key={u.code} value={u.code}>
                      {u.name}
                    </MenuItem>
                  ))}
                </TextField>

                <TextField
                  label="College Code"
                  variant="outlined"
                  size="medium"
                  margin="normal"
                  fullWidth
                  value={collegeCode}
                  onChange={(e) => setCollegeCode(e.target.value.toUpperCase())}
                />
              </>
            )}

            <TextField
              label="Username"
              variant="outlined"
              size="medium"
              margin="normal"
              fullWidth
              value={username}
              onChange={(e) => setUsername(e.target.value)}
            />

            <TextField
              label="Password"
              type={showPassword ? "text" : "password"}
              variant="outlined"
              size="medium"
              margin="normal"
              fullWidth
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showPassword ? "Hide password" : "Show password"}
                        onClick={() => setShowPassword((v) => !v)}
                        onMouseDown={(e) => e.preventDefault()}
                        edge="end"
                        size="small"
                      >
                        {showPassword ? <VisibilityOff /> : <Visibility />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />

            {error && (
              <Typography color="error" variant="body2">
                {error}
              </Typography>
            )}

            <Button type="button" variant="contained" size="large" fullWidth onClick={() => void handleLogin()}>
              {loading ? <CircularProgress size={24} color="inherit" /> : "Login"}
            </Button>
          </Box>
        </Paper>
      </Container>
    </Box>
  );
};

export default Login;
