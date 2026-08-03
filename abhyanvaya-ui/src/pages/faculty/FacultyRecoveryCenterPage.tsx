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
  TextField,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import PendingSessionCard from "../../components/attendance-recovery/PendingSessionCard";
import {
  cancelRecoverySession,
  getFacultyRecoveryCenter,
  getRecoveryPreferences,
  getRetryHistory,
  retryAttendanceSession,
  upsertRecoveryPreferences,
  type AttendanceRecoveryPreference,
  type FacultyRecoveryCenter,
  type PendingAttendanceSession,
} from "../../services/attendanceRecoveryService";

const Section = ({
  title,
  items,
  onRetry,
  onCancel,
  onHistory,
}: {
  title: string;
  items: PendingAttendanceSession[];
  onRetry: (id: string, kind: number) => void;
  onCancel: (id: string) => void;
  onHistory: (id: string) => void;
}) => (
  <Box>
    <Typography variant="h6" sx={{ mb: 1 }}>
      {title} ({items.length})
    </Typography>
    <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
      {items.map((s) => (
        <Box key={s.sessionId} sx={{ position: "relative" }}>
          <PendingSessionCard session={s} onRetry={onRetry} onCancel={onCancel} />
          <Button size="small" sx={{ ml: 1, mb: 1 }} onClick={() => onHistory(s.sessionId)}>
            History
          </Button>
        </Box>
      ))}
    </Stack>
    {items.length === 0 && (
      <Typography variant="body2" color="text.secondary">
        None
      </Typography>
    )}
  </Box>
);

const FacultyRecoveryCenterPage = () => {
  const [data, setData] = useState<FacultyRecoveryCenter | null>(null);
  const [prefs, setPrefs] = useState<AttendanceRecoveryPreference | null>(null);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [msg, setMsg] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [historyText, setHistoryText] = useState("");

  const load = async (q?: string) => {
    setLoading(true);
    setError(null);
    try {
      const [center, preferences] = await Promise.all([
        getFacultyRecoveryCenter(q),
        getRecoveryPreferences(),
      ]);
      setData(center.data);
      setPrefs(preferences.data);
    } catch {
      setError("Failed to load faculty recovery center.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    const onRefresh = () => void load(query || undefined);
    window.addEventListener("attendance-recovery-refresh", onRefresh);
    return () => window.removeEventListener("attendance-recovery-refresh", onRefresh);
  }, []);

  const onRetry = async (id: string, kind: number) => {
    await retryAttendanceSession(id, kind);
    setMsg("Retry queued.");
    await load(query || undefined);
  };

  const onCancel = async (id: string) => {
    await cancelRecoverySession(id);
    setMsg("Session cancelled.");
    await load(query || undefined);
  };

  const onHistory = async (id: string) => {
    const res = await getRetryHistory(id);
    setHistoryText(JSON.stringify(res.data, null, 2));
    setHistoryOpen(true);
  };

  const savePrefs = async () => {
    if (!prefs) return;
    await upsertRecoveryPreferences(prefs);
    setMsg("Recovery preferences saved.");
  };

  if (loading && !data) return <CircularProgress sx={{ m: 2 }} />;

  return (
    <Stack spacing={2} sx={{ p: { xs: 1.5, md: 2 } }}>
      <Box sx={{ display: "flex", gap: 1, alignItems: "center", flexWrap: "wrap" }}>
        <Button component={RouterLink} to="/faculty?tab=pending" startIcon={<ArrowBackIcon />}>
          Faculty
        </Button>
        <Typography variant="h5" sx={{ flexGrow: 1 }}>
          Faculty Recovery Center
        </Typography>
      </Box>
      {error && <Alert severity="error">{error}</Alert>}
      {msg && (
        <Alert severity="success" onClose={() => setMsg(null)}>
          {msg}
        </Alert>
      )}

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
        <TextField
          size="small"
          label="Search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          fullWidth
        />
        <Button variant="contained" onClick={() => void load(query || undefined)}>
          Search
        </Button>
      </Stack>

      {prefs && (
        <Box sx={{ p: 2, border: 1, borderColor: "divider", borderRadius: 1 }}>
          <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
            Recovery preferences
          </Typography>
          <Stack direction={{ xs: "column", md: "row" }} spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            <TextField
              size="small"
              type="number"
              label="Auto-save (sec)"
              value={prefs.autoSaveFrequencySeconds}
              onChange={(e) =>
                setPrefs({ ...prefs, autoSaveFrequencySeconds: Number(e.target.value) || 30 })
              }
            />
            <TextField
              size="small"
              label="Landing page"
              value={prefs.defaultLandingPage}
              onChange={(e) => setPrefs({ ...prefs, defaultLandingPage: e.target.value })}
            />
            <TextField
              size="small"
              type="number"
              label="Timeout warning (min)"
              value={prefs.sessionTimeoutWarningMinutes}
              onChange={(e) =>
                setPrefs({ ...prefs, sessionTimeoutWarningMinutes: Number(e.target.value) || 30 })
              }
            />
            <Button variant="outlined" onClick={() => void savePrefs()}>
              Save preferences
            </Button>
          </Stack>
        </Box>
      )}

      {data && (
        <>
          {data.searchResults.length > 0 && (
            <Section
              title="Search results"
              items={data.searchResults}
              onRetry={onRetry}
              onCancel={onCancel}
              onHistory={onHistory}
            />
          )}
          <Section
            title="Needs attention"
            items={data.needsAttention}
            onRetry={onRetry}
            onCancel={onCancel}
            onHistory={onHistory}
          />
          <Section
            title="Today's sessions"
            items={data.todaysSessions}
            onRetry={onRetry}
            onCancel={onCancel}
            onHistory={onHistory}
          />
          <Section
            title="Yesterday"
            items={data.yesterday}
            onRetry={onRetry}
            onCancel={onCancel}
            onHistory={onHistory}
          />
          <Section
            title="Completed"
            items={data.completed}
            onRetry={onRetry}
            onCancel={onCancel}
            onHistory={onHistory}
          />
          <Section
            title="Archived"
            items={data.archived}
            onRetry={onRetry}
            onCancel={onCancel}
            onHistory={onHistory}
          />
        </>
      )}

      <Dialog open={historyOpen} onClose={() => setHistoryOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Retry history</DialogTitle>
        <DialogContent>
          <Box component="pre" sx={{ whiteSpace: "pre-wrap", fontSize: 12 }}>
            {historyText}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setHistoryOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default FacultyRecoveryCenterPage;
