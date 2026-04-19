import { useState } from "react";
import {
  Container,
  TextField,
  Button,
  Typography,
  Box,
  Paper,
  CircularProgress,
  MenuItem,
} from "@mui/material";
import { getUniversities, login as loginApi, type UniversityOption } from "../services/authService";
import { useAuth } from "../context/AuthContext";
import { useNavigate } from "react-router-dom";
import { useEffect } from "react";

const Login = () => {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [universityCode, setUniversityCode] = useState("");
  const [collegeCode, setCollegeCode] = useState("");
  const [universities, setUniversities] = useState<UniversityOption[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const navigate = useNavigate();
  const { login } = useAuth();

  useEffect(() => {
    const loadUniversities = async () => {
      try {
        const res = await getUniversities();
        setUniversities(res.data);
        if (res.data.length > 0) {
          setUniversityCode(res.data[0].code);
        }
      } catch {
        setError("Failed to load universities");
      }
    };

    void loadUniversities();
  }, []);

  const handleLogin = async () => {
    if (loading) return;

    setLoading(true);
    setError("");

    try {
      const res = await loginApi(universityCode, collegeCode, username, password);
      login(res.data.token);
      navigate("/dashboard");
    } catch {
      setError("Invalid university, college code, username or password");
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

          <Typography variant="subtitle1" color="text.secondary" sx={{ mb: 3 }}>
            Attendance Management System
          </Typography>

          <Box
            sx={{
              display: "flex",
              flexDirection: "column",
              gap: 2,
            }}
          >
            <TextField
              select
              label="University"
              variant="outlined"
              size="medium"
              margin="normal"
              fullWidth
              value={universityCode}
              onChange={(e) => setUniversityCode(e.target.value)}
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
              type="password"
              variant="outlined"
              size="medium"
              margin="normal"
              fullWidth
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />

            {error && (
              <Typography color="error" variant="body2">
                {error}
              </Typography>
            )}

            <Button type="button" variant="contained" size="large" fullWidth onClick={handleLogin}>
              {loading ? <CircularProgress size={24} color="inherit" /> : "Login"}
            </Button>
          </Box>
        </Paper>
      </Container>
    </Box>
  );
};

export default Login;

