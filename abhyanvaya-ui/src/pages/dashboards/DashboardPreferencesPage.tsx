import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import { useAuth } from "../../context/AuthContext";
import {
  getDashboardPreferences,
  upsertDashboardPreferences,
  type DashboardPreferenceDto,
} from "../../services/enterpriseDashboardService";

const FACULTY_WIDGETS = [
  "todays-classes",
  "completed-classes",
  "remaining-classes",
  "todays-students",
  "attendance-completed",
  "pending-attendance",
  "recovery-sessions",
  "recognition-reviews",
  "avg-completion",
  "attendance-percent",
];

const ADMIN_WIDGETS = [
  "pending-attendance",
  "pending-recovery",
  "draft-timetables",
  "published-timetables",
  "conflict-count",
  "optimization-queue",
  "recognition-queue",
  "approval-queue",
  "faculty-online",
  "todays-classes",
  "students-below-threshold",
  "platform-health",
];

/** AI31.6.9 — DB-persisted dashboard preferences. */
const DashboardPreferencesPage = () => {
  const { user } = useAuth();
  const roleScope =
    (user?.role ?? "Faculty").toLowerCase() === "admin" ||
    (user?.role ?? "").toLowerCase() === "superadmin"
      ? "Admin"
      : "Faculty";
  const catalog = roleScope === "Admin" ? ADMIN_WIDGETS : FACULTY_WIDGETS;

  const [prefs, setPrefs] = useState<DashboardPreferenceDto | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void getDashboardPreferences(roleScope)
      .then((r) => setPrefs(r.data))
      .catch(() => setError("Unable to load preferences."));
  }, [roleScope]);

  const save = async () => {
    if (!prefs) return;
    setMessage(null);
    setError(null);
    try {
      const res = await upsertDashboardPreferences({
        roleScope,
        defaultLandingPage: prefs.defaultLandingPage,
        compactMode: prefs.compactMode,
        hiddenWidgets: prefs.hiddenWidgets,
        widgetOrder: prefs.widgetOrder.length ? prefs.widgetOrder : catalog,
      });
      setPrefs(res.data);
      setMessage("Preferences saved to database.");
    } catch {
      setError("Unable to save preferences.");
    }
  };

  const toggleHidden = (code: string) => {
    if (!prefs) return;
    const hidden = new Set(prefs.hiddenWidgets);
    if (hidden.has(code)) hidden.delete(code);
    else hidden.add(code);
    setPrefs({ ...prefs, hiddenWidgets: [...hidden] });
  };

  if (!prefs) {
    return <Typography sx={{ p: 2 }}>{error ?? "Loading preferences…"}</Typography>;
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Dashboard Preferences
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Persisted per user and tenant ({roleScope}). Not localStorage-only.
      </Typography>
      {message && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {message}
        </Alert>
      )}
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Stack spacing={2} sx={{ maxWidth: 560 }}>
        <FormControl fullWidth>
          <InputLabel id="landing">Default landing page</InputLabel>
          <Select
            labelId="landing"
            label="Default landing page"
            value={prefs.defaultLandingPage}
            onChange={(e) => setPrefs({ ...prefs, defaultLandingPage: e.target.value })}
          >
            <MenuItem value="command-center">Faculty Command Center</MenuItem>
            <MenuItem value="faculty-workspace">Faculty Workspace</MenuItem>
            <MenuItem value="admin-operations">Admin Operations</MenuItem>
            <MenuItem value="analytics">Analytics</MenuItem>
            <MenuItem value="health">Health Center</MenuItem>
            <MenuItem value="notifications">Notifications</MenuItem>
          </Select>
        </FormControl>

        <FormControlLabel
          control={
            <Checkbox
              checked={prefs.compactMode}
              onChange={(e) => setPrefs({ ...prefs, compactMode: e.target.checked })}
            />
          }
          label="Compact mode"
        />

        <Typography variant="subtitle1">Hide cards</Typography>
        {catalog.map((code) => (
          <FormControlLabel
            key={code}
            control={
              <Checkbox
                checked={prefs.hiddenWidgets.includes(code)}
                onChange={() => toggleHidden(code)}
              />
            }
            label={`Hide ${code}`}
          />
        ))}

        <Typography variant="subtitle1">Reorder (comma-separated codes)</Typography>
        <FormControl fullWidth>
          <InputLabel id="order">Widget order</InputLabel>
          <Select labelId="order" label="Widget order" value="custom" onChange={() => undefined}>
            <MenuItem value="custom">
              {(prefs.widgetOrder.length ? prefs.widgetOrder : catalog).join(", ")}
            </MenuItem>
          </Select>
        </FormControl>
        <Button
          variant="outlined"
          onClick={() => setPrefs({ ...prefs, widgetOrder: [...catalog].reverse() })}
        >
          Reverse order
        </Button>
        <Button variant="outlined" onClick={() => setPrefs({ ...prefs, widgetOrder: catalog })}>
          Reset order
        </Button>

        <Button variant="contained" onClick={() => void save()}>
          Save preferences
        </Button>
      </Stack>
    </Box>
  );
};

export default DashboardPreferencesPage;
