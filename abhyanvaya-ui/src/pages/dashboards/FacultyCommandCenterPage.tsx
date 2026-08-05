import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useNavigate } from "react-router-dom";
import * as signalR from "@microsoft/signalr";
import { DashboardWidgetGrid } from "../../components/dashboards/DashboardWidgets";
import {
  getFacultyActivityTimeline,
  getFacultyCommandCenter,
  type FacultyActivityTimelineDto,
  type FacultyCommandCenterDto,
} from "../../services/enterpriseDashboardService";

const formatTime = (t?: string | null) => (t ? t.slice(0, 5) : "—");

/** AI31.6.1–4 — Faculty Command Center (does not remove Faculty Workspace). */
const FacultyCommandCenterPage = () => {
  const navigate = useNavigate();
  const [data, setData] = useState<FacultyCommandCenterDto | null>(null);
  const [timeline, setTimeline] = useState<FacultyActivityTimelineDto | null>(null);
  const [range, setRange] = useState("Today");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [liveNote, setLiveNote] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      // Command center already includes activity preview; timeline is additive.
      const cc = await getFacultyCommandCenter();
      setData(cc.data);
      try {
        const tl = await getFacultyActivityTimeline(range);
        setTimeline(tl.data);
      } catch {
        setTimeline({ range, events: cc.data.activityPreview ?? [] });
      }
    } catch (err) {
      if (axios.isAxiosError(err)) {
        const status = err.response?.status;
        setError(`Unable to load Faculty Command Center${status ? ` (HTTP ${status})` : "."}`);
      } else {
        setError("Unable to load Faculty Command Center.");
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [range]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) return;
    const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/api\/?$/, "") ?? "";
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/faculty`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    connection.on("FacultyScheduleNotification", () => {
      setLiveNote("Schedule update received");
      void load();
    });
    connection.on("AttendanceRecoveryNotification", () => {
      setLiveNote("Recovery update received");
      void load();
    });

    void connection.start().catch(() => undefined);
    return () => {
      void connection.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (loading && !data) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1}
        sx={{ justifyContent: "space-between", alignItems: { sm: "center" }, mb: 2 }}
      >
        <Box>
          <Typography variant="h4">Faculty Command Center</Typography>
          <Typography variant="body2" color="text.secondary">
            Operational home — Faculty Workspace remains at /faculty. Mode: {data?.mode ?? "—"}
            {data?.hasTimetable ? " (timetable)" : " (legacy selectors via resolver)"}
          </Typography>
        </Box>
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          <Button variant="outlined" onClick={() => navigate("/faculty")}>
            Faculty Workspace
          </Button>
          <Button variant="outlined" onClick={() => navigate("/dashboard/preferences")}>
            Preferences
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {liveNote && (
        <Alert severity="info" sx={{ mb: 2 }} onClose={() => setLiveNote(null)}>
          {liveNote}
        </Alert>
      )}
      {data?.message && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {data.message}
        </Alert>
      )}

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", md: "1fr 1fr 1fr" },
          gap: 2,
          mb: 2,
        }}
      >
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" color="text.secondary">
            Current Class
          </Typography>
          <Typography variant="h6">{data?.currentClass?.subjectName ?? "None"}</Typography>
          <Typography variant="body2">
            {formatTime(data?.currentClass?.startTime)} – {formatTime(data?.currentClass?.endTime)} ·{" "}
            {data?.currentClass?.roomName ?? "—"}
          </Typography>
        </Paper>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" color="text.secondary">
            Next Class
          </Typography>
          <Typography variant="h6">{data?.nextClass?.subjectName ?? "None"}</Typography>
          <Typography variant="body2">
            {formatTime(data?.nextClass?.startTime)} – {formatTime(data?.nextClass?.endTime)} ·{" "}
            {data?.nextClass?.roomName ?? "—"}
          </Typography>
        </Paper>
        <Paper sx={{ p: 2 }}>
          <Typography variant="subtitle2" color="text.secondary">
            Today
          </Typography>
          <Typography variant="body1">Classes: {data?.todaysClasses.length ?? 0}</Typography>
          <Typography variant="body1">Remaining: {data?.remainingClasses ?? 0}</Typography>
          <Typography variant="body1">Students: {data?.todaysStudents ?? 0}</Typography>
          <Typography variant="body1">
            Pending: {data?.attendancePending ?? 0} · Recovery: {data?.recoveryQueue ?? 0}
          </Typography>
        </Paper>
      </Box>

      <Typography variant="h6" gutterBottom>
        Quick Actions
      </Typography>
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", mb: 3 }}>
        {(data?.quickActions ?? []).map((a) => (
          <Button key={a.code} variant={a.primary ? "contained" : "outlined"} onClick={() => navigate(a.path)}>
            {a.label}
          </Button>
        ))}
      </Stack>

      <Typography variant="h6" gutterBottom>
        KPIs
      </Typography>
      <Box sx={{ mb: 3 }}>
        <DashboardWidgetGrid widgets={data?.widgets ?? []} compact={data?.preferences.compactMode} />
      </Box>

      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", lg: "1fr 1fr" },
          gap: 2,
          mb: 2,
        }}
      >
        <Paper sx={{ p: 2 }}>
          <Typography variant="h6" gutterBottom>
            AI Insights
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
            Composed from existing analytics — never generates AI content.
          </Typography>
          <Stack spacing={1}>
            {(data?.insights.items ?? []).map((i) => (
              <Alert
                key={i.code}
                severity={i.severity === "Critical" ? "error" : i.severity === "Warning" ? "warning" : "info"}
                action={
                  i.path ? (
                    <Button color="inherit" size="small" onClick={() => navigate(i.path!)}>
                      Open
                    </Button>
                  ) : undefined
                }
              >
                <strong>{i.title}</strong> — {i.message}
              </Alert>
            ))}
          </Stack>
        </Paper>

        <Paper sx={{ p: 2 }}>
          <Stack direction="row" spacing={1} sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
            <Typography variant="h6">Activity Timeline</Typography>
            <FormControl size="small" sx={{ minWidth: 120 }}>
              <InputLabel id="tl-range">Range</InputLabel>
              <Select labelId="tl-range" label="Range" value={range} onChange={(e) => setRange(e.target.value)}>
                <MenuItem value="Today">Today</MenuItem>
                <MenuItem value="Week">Week</MenuItem>
                <MenuItem value="Month">Month</MenuItem>
              </Select>
            </FormControl>
          </Stack>
          <Stack spacing={1}>
            {(timeline?.events ?? data?.activityPreview ?? []).map((e) => (
              <Paper key={e.eventId} variant="outlined" sx={{ p: 1.5 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5 }}>
                  <Chip size="small" label={e.kind} />
                  <Typography variant="caption" color="text.secondary">
                    {new Date(e.occurredUtc).toLocaleString()}
                  </Typography>
                </Stack>
                <Typography variant="subtitle2">{e.title}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {e.message}
                </Typography>
              </Paper>
            ))}
            {(timeline?.events.length ?? 0) === 0 && (
              <Typography variant="body2" color="text.secondary">
                No activity for this range.
              </Typography>
            )}
          </Stack>
        </Paper>
      </Box>

      <Typography variant="h6" gutterBottom>
        Today&apos;s Classes
      </Typography>
      <Stack spacing={1}>
        {(data?.todaysClasses ?? []).map((c, idx) => (
          <Paper key={`${c.subjectName}-${idx}`} sx={{ p: 1.5 }}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={1}
              sx={{ justifyContent: "space-between" }}
            >
              <Box>
                <Typography sx={{ fontWeight: 600 }}>{c.subjectName ?? "Class"}</Typography>
                <Typography variant="body2">
                  {formatTime(c.startTime)} – {formatTime(c.endTime)} · {c.roomName ?? "—"} · {c.status}
                </Typography>
              </Box>
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Chip size="small" label={c.attendanceStatus} />
                <Button
                  size="small"
                  variant="contained"
                  onClick={() => navigate(c.takeAttendancePath || "/attendance")}
                >
                  Attendance
                </Button>
              </Stack>
            </Stack>
          </Paper>
        ))}
      </Stack>
    </Box>
  );
};

export default FacultyCommandCenterPage;
