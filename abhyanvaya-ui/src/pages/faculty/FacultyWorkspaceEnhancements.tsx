import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  LinearProgress,
  MenuItem,
  Skeleton,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {
  downloadFacultyCalendarIcs,
  facultyCalendarSubscribeUrl,
  getClassroomNavigation,
  getFacultyProductivity,
  getFacultyProductivityDashboard,
  getFacultyTimeline,
  getSmartFacultyNotifications,
  getWorkspacePreferences,
  searchFacultyWorkspace,
  updateWorkspacePreferences,
  type ClassroomNavigationDto,
  type FacultyAttendanceProductivityDto,
  type FacultyProductivityDashboardDto,
  type FacultySearchResponseDto,
  type FacultySmartNotificationsDto,
  type FacultyTimelineDto,
  type WorkspacePreferenceDto,
} from "../../services/facultyWorkspaceService";

const ProductivityCharts = lazy(() => import("./FacultyProductivityCharts"));

const formatTime = (t?: string | null) => (t ? String(t).slice(0, 5) : "—");

type Props = {
  tab: string;
  touchSx: object;
  oneHanded: boolean;
  highContrast: boolean;
  onNavigate: (path: string) => void;
  onPreferencesLoaded?: (prefs: WorkspacePreferenceDto) => void;
};

export const FacultyTimelinePanel = ({
  touchSx,
  onNavigate,
  swipeIndex,
  onSwipeIndex,
}: {
  touchSx: object;
  onNavigate: (path: string) => void;
  swipeIndex: number;
  onSwipeIndex: (i: number) => void;
}) => {
  const [data, setData] = useState<FacultyTimelineDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [touchX, setTouchX] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    void getFacultyTimeline()
      .then((res) => {
        if (!cancelled) setData(res.data);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const classes = useMemo(() => (data?.items ?? []).filter((i) => i.kind === "Class"), [data]);
  const focused = classes[swipeIndex] ?? classes[0];

  if (loading) return <Skeleton variant="rounded" height={180} />;
  if (!data) return <Alert severity="info">No timeline data.</Alert>;

  return (
    <Stack spacing={1.5} role="list" aria-label="Daily timeline" onTouchStart={(e) => setTouchX(e.changedTouches[0]?.clientX ?? null)}
      onTouchEnd={(e) => {
        if (touchX == null) return;
        const dx = (e.changedTouches[0]?.clientX ?? touchX) - touchX;
        if (Math.abs(dx) > 48) {
          const next = dx < 0 ? swipeIndex + 1 : swipeIndex - 1;
          onSwipeIndex(Math.max(0, Math.min(classes.length - 1, next)));
        }
        setTouchX(null);
      }}
    >
      <Typography variant="subtitle2">Timeline · {data.date}</Typography>
      {focused && (
        <Alert severity="success" aria-live="polite">
          Swipe focus: {focused.label} ({formatTime(focused.startTime)}–{formatTime(focused.endTime)})
        </Alert>
      )}
      {data.items.map((item, idx) => (
        <Box key={`${item.kind}-${item.startTime}-${idx}`} role="listitem" sx={{p: 1.5, borderLeft: 4, borderColor: item.kind === "Break" ? "grey.400" : item.status === "Current" ? "success.main" : "primary.main", bgcolor: item.status === "Current" ? "success.50" : "background.paper"}}>
          <Typography sx={{ fontWeight: 700 }}>
            {formatTime(item.startTime)}–{formatTime(item.endTime)} · {item.label}
          </Typography>
          <Typography variant="body2">
            {item.kind === "Break"
              ? "Break"
              : `${item.roomName ?? "—"} · ${item.buildingName ?? "—"} · Attendance ${item.attendanceStatus}${
                  item.aiReviewPending ? " · AI review pending" : ""
                }`}
          </Typography>
          {item.class?.roomId && (
            <Button size="small" sx={touchSx} onClick={() => onNavigate(`/faculty?tab=navigation&roomId=${item.class!.roomId}`)}>
              Room details
            </Button>
          )}
          {item.kind === "Class" && item.attendanceStatus !== "Completed" && (
            <Button size="small" variant="contained" sx={{ ...touchSx, ml: 1 }} onClick={() => onNavigate("/attendance")}>
              Quick attendance
            </Button>
          )}
        </Box>
      ))}
    </Stack>
  );
};

export const FacultyCalendarPanel = ({ touchSx }: { touchSx: object }) => {
  const [msg, setMsg] = useState<string | null>(null);
  const subscribeUrl = facultyCalendarSubscribeUrl();

  const exportIcs = async () => {
    const res = await downloadFacultyCalendarIcs();
    const url = URL.createObjectURL(res.data);
    const a = document.createElement("a");
    a.href = url;
    a.download = "faculty-calendar.ics";
    a.click();
    URL.revokeObjectURL(url);
    setMsg("ICS downloaded (export-only, no sync).");
  };

  return (
    <Stack spacing={1.5} className="faculty-print-area">
      <Alert severity="info">Calendar integration is export-only. No two-way synchronization.</Alert>
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
        <Button variant="contained" sx={touchSx} onClick={() => void exportIcs()}>
          Export ICS
        </Button>
        <Button variant="outlined" sx={touchSx} onClick={() => window.print()}>
          Print / PDF
        </Button>
      </Stack>
      <Typography variant="body2">Outlook / Google subscription URL (export feed):</Typography>
      <TextField size="small" fullWidth value={subscribeUrl} slotProps={{ input: { readOnly: true } }} aria-label="Calendar subscribe URL" />
      {msg && <Alert severity="success">{msg}</Alert>}
    </Stack>
  );
};

export const FacultyNavigationPanel = ({
  roomId,
  fromRoomId,
  touchSx,
}: {
  roomId?: number;
  fromRoomId?: number;
  touchSx: object;
}) => {
  const [data, setData] = useState<ClassroomNavigationDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!roomId) return;
    void getClassroomNavigation(roomId, fromRoomId ? { fromRoomId } : undefined)
      .then((res) => setData(res.data))
      .catch(() => setError("Room not found"));
  }, [roomId, fromRoomId]);

  if (!roomId) return <Alert severity="info">Open a class room details action to load navigation.</Alert>;
  if (error) return <Alert severity="warning">{error}</Alert>;
  if (!data) return <Skeleton variant="rounded" height={160} />;

  return (
    <Stack spacing={1} aria-label="Classroom navigation">
      <Typography variant="h6">
        {data.roomName} ({data.roomCode})
      </Typography>
      <Typography variant="body2">
        {data.campusName} · {data.buildingName} · {data.floorName} (L{data.floorLevel})
      </Typography>
      <Typography variant="body2">Capacity {data.capacity} · Type {data.roomType}</Typography>
      <Typography variant="body2">Features: {data.features.join(", ") || "None"}</Typography>
      <Typography variant="body2">
        Accessibility: {data.accessibilityFriendly ? "Feature-assisted" : "Standard"} · Walk estimate{" "}
        {data.walkingEstimateMinutes ?? "—"} min
      </Typography>
      <Alert severity="info">{data.directionsPlaceholder}</Alert>
      <Button sx={touchSx} disabled>
        Directions (future)
      </Button>
    </Stack>
  );
};

export const FacultyPreferencesPanel = ({
  touchSx,
  onPreferencesLoaded,
}: {
  touchSx: object;
  onPreferencesLoaded?: (prefs: WorkspacePreferenceDto) => void;
}) => {
  const [prefs, setPrefs] = useState<WorkspacePreferenceDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  useEffect(() => {
    void getWorkspacePreferences().then((res) => {
      setPrefs(res.data);
      onPreferencesLoaded?.(res.data);
    });
  }, [onPreferencesLoaded]);

  if (!prefs) return <Skeleton variant="rounded" height={200} />;

  const save = async () => {
    setSaving(true);
    try {
      const res = await updateWorkspacePreferences({
        landingPage: prefs.landingPage,
        dashboardLayout: prefs.dashboardLayout,
        defaultTimetableView: prefs.defaultTimetableView,
        favoriteQuickActions: prefs.favoriteQuickActions,
        themePreference: prefs.themePreference,
        notificationPreferences: prefs.notificationPreferences,
        oneHandedMode: prefs.oneHandedMode,
        highContrast: prefs.highContrast,
      });
      setPrefs(res.data);
      onPreferencesLoaded?.(res.data);
      setMsg("Preferences saved (per faculty).");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Stack spacing={1.5} component="form" aria-label="Workspace preferences">
      <TextField
        select
        label="Landing page"
        value={prefs.landingPage}
        onChange={(e) => setPrefs({ ...prefs, landingPage: e.target.value })}
      >
        {["home", "timeline", "class", "timetable", "productivity", "notifications"].map((v) => (
          <MenuItem key={v} value={v}>
            {v}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        label="Dashboard layout"
        value={prefs.dashboardLayout}
        onChange={(e) => setPrefs({ ...prefs, dashboardLayout: e.target.value })}
      >
        {["compact", "comfortable", "focus"].map((v) => (
          <MenuItem key={v} value={v}>
            {v}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        label="Default timetable view"
        value={prefs.defaultTimetableView}
        onChange={(e) => setPrefs({ ...prefs, defaultTimetableView: e.target.value })}
      >
        {["Today", "Week", "Month", "Agenda"].map((v) => (
          <MenuItem key={v} value={v}>
            {v}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        label="Theme"
        value={prefs.themePreference}
        onChange={(e) => setPrefs({ ...prefs, themePreference: e.target.value })}
      >
        {["system", "light", "dark", "highContrast"].map((v) => (
          <MenuItem key={v} value={v}>
            {v}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        label="Favorite quick actions (CSV codes)"
        value={prefs.favoriteQuickActions.join(",")}
        onChange={(e) =>
          setPrefs({
            ...prefs,
            favoriteQuickActions: e.target.value.split(",").map((x) => x.trim()).filter(Boolean),
          })
        }
      />
      <FormControlLabel
        control={
          <Checkbox
            checked={prefs.oneHandedMode}
            onChange={(_, v) => setPrefs({ ...prefs, oneHandedMode: v })}
          />
        }
        label="One-handed mode"
      />
      <FormControlLabel
        control={
          <Checkbox checked={prefs.highContrast} onChange={(_, v) => setPrefs({ ...prefs, highContrast: v })} />
        }
        label="High contrast"
      />
      <Typography variant="subtitle2">Notification preferences</Typography>
      {Object.entries(prefs.notificationPreferences).map(([key, value]) => (
        <FormControlLabel
          key={key}
          control={
            <Checkbox
              checked={value}
              onChange={(_, v) =>
                setPrefs({
                  ...prefs,
                  notificationPreferences: { ...prefs.notificationPreferences, [key]: v },
                })
              }
            />
          }
          label={key}
        />
      ))}
      <Button variant="contained" sx={touchSx} disabled={saving} onClick={() => void save()}>
        Save preferences
      </Button>
      {msg && <Alert severity="success">{msg}</Alert>}
    </Stack>
  );
};

export const FacultyProductivityPanel = ({
  touchSx,
  onNavigate,
}: {
  touchSx: object;
  onNavigate: (path: string) => void;
}) => {
  const [prod, setProd] = useState<FacultyAttendanceProductivityDto | null>(null);
  const [dash, setDash] = useState<FacultyProductivityDashboardDto | null>(null);

  useEffect(() => {
    void Promise.all([getFacultyProductivity(), getFacultyProductivityDashboard()]).then(([p, d]) => {
      setProd(p.data);
      setDash(d.data);
    });
  }, []);

  if (!prod || !dash) return <Skeleton variant="rounded" height={240} />;

  return (
    <Stack spacing={2}>
      <Typography variant="h6">Attendance productivity</Typography>
      <Typography variant="body2">
        Pending {prod.pendingAttendance} · Remaining {prod.remainingClasses} · Completion{" "}
        {prod.attendanceCompletionPercent}% · AI reviews {prod.aiPendingReviews} · Missed {prod.missedAttendance} · Late{" "}
        {prod.lateAttendance}
      </Typography>
      <LinearProgress variant="determinate" value={Math.min(100, prod.attendanceCompletionPercent)} />
      {prod.quickResumePath && (
        <Button variant="contained" sx={touchSx} onClick={() => onNavigate(prod.quickResumePath!)}>
          Quick resume
        </Button>
      )}
      <Suspense fallback={<Skeleton variant="rounded" height={200} />}>
        <ProductivityCharts data={dash} />
      </Suspense>
    </Stack>
  );
};

export const FacultySearchPanel = ({
  touchSx,
  onNavigate,
}: {
  touchSx: object;
  onNavigate: (path: string) => void;
}) => {
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(false);
  const [results, setResults] = useState<FacultySearchResponseDto["results"]>([]);

  const run = async () => {
    if (q.trim().length < 2) return;
    setLoading(true);
    try {
      const res = await searchFacultyWorkspace(q.trim());
      setResults(res.data.results);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Stack spacing={1.5} role="search" aria-label="Faculty workspace search">
      <Stack direction="row" spacing={1}>
        <TextField
          fullWidth
          size="small"
          label="Search students, subjects, rooms…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") void run();
          }}
        />
        <Button variant="contained" sx={touchSx} onClick={() => void run()} disabled={loading}>
          Search
        </Button>
      </Stack>
      {loading && <LinearProgress />}
      {results.map((r) => (
        <Button
          key={`${r.category}-${r.entityKey}-${r.title}`}
          variant="outlined"
          sx={{ ...touchSx, justifyContent: "flex-start" }}
          onClick={() => onNavigate(r.navigationPath)}
        >
          [{r.category}] {r.title} — {r.subtitle}
        </Button>
      ))}
    </Stack>
  );
};

export const FacultySmartNotificationsPanel = () => {
  const [data, setData] = useState<FacultySmartNotificationsDto | null>(null);
  useEffect(() => {
    void getSmartFacultyNotifications().then((res) => setData(res.data));
  }, []);
  if (!data) return <Skeleton variant="rounded" height={120} />;
  return (
    <Stack spacing={1} aria-live="polite">
      <Typography variant="caption">
        Smart notifications · SignalR {data.usesSignalR ? "on" : "off"} · Polling {data.usesPolling ? "yes" : "no"}
      </Typography>
      {data.items.length === 0 ? (
        <Typography color="text.secondary">No smart notifications.</Typography>
      ) : (
        data.items.map((n) => (
          <Box key={n.notificationId} sx={{p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 2}}>
            <Typography sx={{ fontWeight: 700 }}>
              {n.kind}: {n.title}
            </Typography>
            <Typography variant="body2">{n.message}</Typography>
          </Box>
        ))
      )}
    </Stack>
  );
};

// silence unused Props export path for lint tooling in some configs
export type FacultyEnhancementHostProps = Props;
