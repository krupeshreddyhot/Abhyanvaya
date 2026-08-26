import { useCallback, useEffect, useState } from "react";
import { Link as RouterLink, useNavigate, useSearchParams } from "react-router-dom";
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  LinearProgress,
  Stack,
  Tab,
  Tabs,
  Typography,
  useMediaQuery,
  useTheme,
} from "@mui/material";
import * as signalR from "@microsoft/signalr";
import {
  AcademicContextBreadcrumb,
  AcademicHelpHint,
  academicChipSx,
  academicPageShellSx,
  academicPanelSx,
  academicTouchButtonSx,
} from "../../components/academic";
import { useAuth } from "../../context/AuthContext";
import {
  decideAutoResume,
  getAutoResumePrompt,
  getWorkspaceRecoverySummary,
  retryAttendanceSession,
  AttendanceRetryKind,
  type AutoResumePrompt,
  type FacultyWorkspaceRecoverySummary,
} from "../../services/attendanceRecoveryService";
import PendingSessionCard from "../../components/attendance-recovery/PendingSessionCard";
import FacultyPendingAttendancePanel from "./FacultyPendingAttendancePanel";
import {
  getFacultyCurrentClass,
  getFacultyInsights,
  getFacultyNotifications,
  getFacultyTimetable,
  getFacultyToday,
  getWorkspacePreferences,
  type FacultyClassDto,
  type FacultyCurrentClassWorkspaceDto,
  type FacultyInsightsDto,
  type FacultyScheduleNotificationDto,
  type FacultyTimetableViewDto,
  type FacultyTodayDto,
  type WorkspacePreferenceDto,
} from "../../services/facultyWorkspaceService";
import {
  FacultyCalendarPanel,
  FacultyNavigationPanel,
  FacultyPreferencesPanel,
  FacultyProductivityPanel,
  FacultySearchPanel,
  FacultySmartNotificationsPanel,
  FacultyTimelinePanel,
} from "./FacultyWorkspaceEnhancements";

const formatTime = (t?: string | null) => {
  if (!t) return "—";
  // ASP.NET TimeSpan often serializes as "HH:mm:ss"
  return String(t).slice(0, 5);
};

const statusColor = (status: string) =>
  status === "Current" ? "success" : status === "Completed" ? "default" : "info";

const FacultyWorkspacePage = () => {
  const theme = useTheme();
  const isPhone = useMediaQuery(theme.breakpoints.down("sm"));
  const isTablet = useMediaQuery(theme.breakpoints.between("sm", "md"));
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") ?? "home";
  const { user, hasPermission } = useAuth();
  const [today, setToday] = useState<FacultyTodayDto | null>(null);
  const [current, setCurrent] = useState<FacultyCurrentClassWorkspaceDto | null>(null);
  const [timetable, setTimetable] = useState<FacultyTimetableViewDto | null>(null);
  const [insights, setInsights] = useState<FacultyInsightsDto | null>(null);
  const [notifications, setNotifications] = useState<FacultyScheduleNotificationDto[]>([]);
  const [timetableView, setTimetableView] = useState("Today");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [online, setOnline] = useState(typeof navigator === "undefined" ? true : navigator.onLine);
  const [lastUpdated, setLastUpdated] = useState<string | null>(null);
  const [liveNote, setLiveNote] = useState<string | null>(null);
  const [prefs, setPrefs] = useState<WorkspacePreferenceDto | null>(null);
  const [swipeIndex, setSwipeIndex] = useState(0);
  const [autoResume, setAutoResume] = useState<AutoResumePrompt | null>(null);
  const [recoverySummary, setRecoverySummary] = useState<FacultyWorkspaceRecoverySummary | null>(null);
  const roomIdParam = params.get("roomId") ? Number(params.get("roomId")) : undefined;

  const setTab = (value: string) => {
    const next = new URLSearchParams(params);
    next.set("tab", value);
    setParams(next, { replace: true });
  };

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [t, c, i, n, recovery] = await Promise.all([
        getFacultyToday(),
        getFacultyCurrentClass(),
        getFacultyInsights(),
        getFacultyNotifications(),
        getWorkspaceRecoverySummary().catch(() => null),
      ]);
      setToday(t.data);
      setCurrent(c.data);
      setInsights(i.data);
      setNotifications(n.data);
      setRecoverySummary(recovery?.data ?? null);
      setLastUpdated(new Date().toLocaleTimeString());
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : "Failed to load faculty workspace");
    } finally {
      setLoading(false);
    }
  }, []);

  const loadTimetable = useCallback(async (view: string) => {
    const res = await getFacultyTimetable({ view });
    setTimetable(res.data);
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void (async () => {
      try {
        const res = await getAutoResumePrompt();
        if (res.data.shouldPrompt) setAutoResume(res.data);
      } catch {
        /* non-blocking */
      }
    })();
  }, []);

  useEffect(() => {
    void getWorkspacePreferences()
      .then((res) => {
        setPrefs(res.data);
        if (!params.get("tab") && res.data.landingPage) {
          setTab(res.data.landingPage === "home" ? "home" : res.data.landingPage);
        }
        if (res.data.defaultTimetableView) setTimetableView(res.data.defaultTimetableView);
      })
      .catch(() => {
        /* optional for non-staff */
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (tab === "timetable") void loadTimetable(timetableView);
  }, [tab, timetableView, loadTimetable]);

  useEffect(() => {
    const on = () => setOnline(true);
    const off = () => setOnline(false);
    window.addEventListener("online", on);
    window.addEventListener("offline", off);
    return () => {
      window.removeEventListener("online", on);
      window.removeEventListener("offline", off);
    };
  }, []);

  useEffect(() => {
    if (!user?.tenantId && !user?.staffId) return;
    const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/api\/?$/, "") ?? "";
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/faculty`, { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on("FacultyScheduleNotification", (n: FacultyScheduleNotificationDto) => {
      setNotifications((prev) => [n, ...prev].slice(0, 40));
      setLiveNote(`${n.kind}: ${n.message}`);
    });

    // AI22.8 — recovery lifecycle events (recognition/review/finalize); no polling.
    connection.on(
      "AttendanceRecoveryNotification",
      (msg: { eventName?: string; payload?: { sessionId?: string; workflowStatus?: string; resumePath?: string } }) => {
        const eventName = msg?.eventName ?? "Recovery";
        const status = msg?.payload?.workflowStatus ?? "";
        const sessionShort = msg?.payload?.sessionId?.slice(0, 8) ?? "";
        setLiveNote(
          sessionShort
            ? `Attendance ${eventName}${status ? ` · ${status}` : ""} · ${sessionShort}…`
            : `Attendance ${eventName}`,
        );
        window.dispatchEvent(new CustomEvent("attendance-recovery-refresh"));
      },
    );

    void connection
      .start()
      .then(async () => {
        if (user.tenantId) await connection.invoke("SubscribeTenant", user.tenantId);
        if (user.staffId) await connection.invoke("SubscribeStaff", user.staffId);
      })
      .catch(() => {
        /* hub optional */
      });

    return () => {
      void connection.stop();
    };
  }, [user?.tenantId, user?.staffId]);

  const openAttendance = (ai = false) => {
    // Explicit ai query so AttendanceMarking selects AI Photo vs Manual regardless of last session selection.
    navigate(ai ? "/attendance?ai=1" : "/attendance?ai=0", {
      state: { attendanceMethod: ai ? "aiPhoto" : "manual" },
    });
  };

  const openReview = (path: string) => navigate(path);

  const oneHanded = prefs?.oneHandedMode ?? false;
  const highContrast = prefs?.highContrast || prefs?.themePreference === "highContrast";
  const touchSx = {
    minHeight: isPhone || isTablet || oneHanded ? 56 : 42,
    px: oneHanded ? 3 : 2.5,
    fontSize: isPhone || oneHanded ? "1.05rem" : undefined,
  };

  const schedule = today?.todaysSchedule ?? [];
  const quickActions = (today?.quickActions ?? current?.quickActions ?? []).filter((a) => {
    if (!prefs?.favoriteQuickActions?.length) return true;
    return prefs.favoriteQuickActions.includes(a.code) || a.primary;
  });

  const offlineBanner = !online && (
    <Alert severity="warning">
      Connection lost — status only. Offline attendance is not available. Last updated: {lastUpdated ?? "—"}.{" "}
      <Button size="small" onClick={() => void load()} disabled={!online}>
        Reconnect
      </Button>
    </Alert>
  );

  const classCard = (c: FacultyClassDto) => (
    <Box
      key={`${c.timetableEntryId}-${c.startTime}-${c.status}`}
      sx={{
        ...academicPanelSx(c.status === "Current" ? "attendance" : "context"),
        bgcolor: c.status === "Current" ? "success.50" : "background.paper",
        mb: 1,
      }}
    >
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <Chip
          size="small"
          color={statusColor(c.status) as "success" | "default" | "info"}
          label={c.status}
          sx={academicChipSx}
        />
        <Typography sx={{ fontWeight: 700 }}>{c.subjectName ?? `Subject #${c.subjectId}`}</Typography>
        <Typography color="text.secondary">
          {formatTime(c.startTime)}–{formatTime(c.endTime)}
        </Typography>
      </Stack>
      <Typography variant="body2" sx={{ mt: 0.5 }}>
        Room {c.roomName ?? "—"}
        {c.buildingName ? ` · ${c.buildingName}` : ""} · Students {c.studentCount ?? "—"} · Attendance{" "}
        {c.attendanceStatus}
        {c.aiCaptureStatus ? ` · AI ${c.aiCaptureStatus}` : ""}
        {c.minutesRemaining != null ? ` · ${c.minutesRemaining} min left` : ""}
      </Typography>
    </Box>
  );

  const stickyActions = (
    <Box sx={{position: isPhone ? "sticky" : "static", bottom: 0, zIndex: 2, py: 1.5, px: isPhone ? 0 : 0, bgcolor: "background.default", borderTop: isPhone ? "1px solid" : "none", borderColor: "divider", pb: isPhone ? "calc(12px + env(safe-area-inset-bottom))" : 1.5}}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
        {quickActions
          .filter((a) => a.enabled)
          .map((a) => (
            <Button
              key={a.code}
              variant={a.primary ? "contained" : "outlined"}
              sx={touchSx}
              onClick={() => {
                if (a.code === "TAKE_ATTENDANCE") openAttendance(false);
                else if (a.code === "AI_ATTENDANCE") openAttendance(true);
                else navigate(a.path);
              }}
            >
              {a.label}
            </Button>
          ))}
      </Stack>
    </Box>
  );

  return (
    <Stack
      spacing={1.25}
      component="main"
      aria-label="Faculty workspace"
      sx={{
        ...academicPageShellSx,
        pb: isPhone ? 10 : 2,
        pt: "env(safe-area-inset-top)",
        ...(highContrast
          ? {
              bgcolor: "#000",
              color: "#fff",
              "& .MuiTypography-root": { color: "#fff" },
              "& .MuiAlert-root": { border: "1px solid #fff" },
            }
          : {}),
        ...(prefs?.dashboardLayout === "compact" ? { gap: 1 } : {}),
        ...(isTablet ? { maxWidth: 980 } : {}),
      }}
    >
      <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", alignItems: "center" }}>
        <Typography variant={isPhone ? "h5" : "h4"} sx={{ flexGrow: 1, fontWeight: 800 }}>
          Faculty Workspace
        </Typography>
        <AcademicHelpHint
          title="Faculty operations"
          body="Mobile and tablet prioritize today’s classes, attendance actions, and recovery. Desktop adds insights and preferences."
        />
        <Chip
          size="small"
          label={online ? "Online" : "Offline"}
          color={online ? "success" : "warning"}
          sx={academicChipSx}
        />
        {lastUpdated && (
          <Typography variant="caption" color="text.secondary">
            Updated {lastUpdated}
          </Typography>
        )}
        <Button variant="outlined" size="small" sx={{ ...touchSx, ...academicTouchButtonSx }} component={RouterLink} to="/faculty/recovery">
          Recovery center
        </Button>
        <Button variant="outlined" size="small" sx={{ ...touchSx, ...academicTouchButtonSx }} onClick={() => void load()} disabled={!online}>
          Refresh
        </Button>
      </Stack>
      <AcademicContextBreadcrumb
        context={
          current?.currentClass
            ? {
                courseId: current.currentClass.courseId,
                groupId: current.currentClass.groupId,
                semesterId: current.currentClass.semesterId,
                subjectId: current.currentClass.subjectId,
              }
            : today?.currentClass
              ? {
                  courseId: today.currentClass.courseId,
                  groupId: today.currentClass.groupId,
                  semesterId: today.currentClass.semesterId,
                  subjectId: today.currentClass.subjectId,
                }
              : null
        }
      />

      {offlineBanner}
      {recoverySummary && (
        <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
          {[
            ["Today's classes", recoverySummary.todaysClasses],
            ["Pending attendance", recoverySummary.pendingAttendance],
            ["Needs review", recoverySummary.needsReview],
            ["Recognition running", recoverySummary.recognitionRunning],
            ["Completed today", recoverySummary.completedToday ?? recoverySummary.completed],
            [
              "Avg review",
              recoverySummary.averageReviewTimeMinutes != null
                ? `${recoverySummary.averageReviewTimeMinutes.toFixed(0)}m`
                : "—",
            ],
          ].map(([label, value]) => (
            <Chip
              key={String(label)}
              label={`${label}: ${value}`}
              color={label === "Needs review" || label === "Pending attendance" ? "warning" : "default"}
              onClick={() => setTab(label === "Pending attendance" || label === "Needs review" ? "pending" : "home")}
            />
          ))}
          {(recoverySummary.slaDistribution ?? []).map((s) => (
            <Chip
              key={`sla-${s.label}`}
              size="small"
              variant="outlined"
              color={
                s.label === "Red"
                  ? "error"
                  : s.label === "Orange"
                    ? "warning"
                    : s.label === "Yellow"
                      ? "warning"
                      : "success"
              }
              label={`SLA ${s.label}: ${s.value}`}
              onClick={() => setTab("pending")}
            />
          ))}
          {(recoverySummary.pendingByPriority ?? []).slice(0, 4).map((p) => (
            <Chip
              key={`pri-${p.label}`}
              size="small"
              variant="outlined"
              label={`${p.label}: ${p.value}`}
              onClick={() => setTab("pending")}
            />
          ))}
        </Stack>
      )}
      {recoverySummary && recoverySummary.topPending.length > 0 && tab === "home" && (
        <Box>
          <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 700 }}>
            Pending attendance (quick actions)
          </Typography>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            {recoverySummary.topPending.slice(0, 3).map((s) => (
              <PendingSessionCard
                key={s.sessionId}
                session={s}
                touchSx={touchSx}
                compact
                onRetry={(id, kind) => void retryAttendanceSession(id, kind)}
              />
            ))}
          </Stack>
          <Stack direction="row" spacing={1} sx={{ mt: 1, flexWrap: "wrap" }}>
            <Button size="small" sx={touchSx} onClick={() => setTab("pending")}>
              Resume queue
            </Button>
            <Button
              size="small"
              sx={touchSx}
              onClick={() => {
                const failed = recoverySummary.topPending.find((s) => s.canRetry);
                if (failed) void retryAttendanceSession(failed.sessionId, AttendanceRetryKind.RetryRecognition);
              }}
            >
              Retry
            </Button>
            <Button
              size="small"
              sx={touchSx}
              onClick={() => {
                const ready = recoverySummary.topPending.find((s) => s.canFinalize);
                if (ready) openReview(ready.resumePath);
              }}
            >
              Finalize
            </Button>
            <Button size="small" sx={touchSx} component={RouterLink} to="/faculty/recovery">
              View history
            </Button>
          </Stack>
        </Box>
      )}
      {liveNote && (
        <Alert severity="info" onClose={() => setLiveNote(null)}>
          {liveNote}
        </Alert>
      )}
      {error && <Alert severity="error">{error}</Alert>}
      {loading && <LinearProgress />}

      <Tabs
        value={tab}
        onChange={(_, v) => setTab(v)}
        variant="scrollable"
        allowScrollButtonsMobile
        aria-label="Faculty workspace sections"
      >
        <Tab value="home" label="Today" />
        <Tab value="pending" label="Pending attendance" />
        <Tab value="timeline" label="Timeline" />
        <Tab value="class" label="Current class" />
        <Tab value="timetable" label="Timetable" />
        <Tab value="calendar" label="Calendar" />
        <Tab value="productivity" label="Productivity" />
        <Tab value="search" label="Search" />
        <Tab value="navigation" label="Room" />
        <Tab value="preferences" label="Preferences" />
        <Tab value="insights" label="Insights" />
        <Tab value="notifications" label="Notifications" />
      </Tabs>

      {tab === "pending" && <FacultyPendingAttendancePanel touchSx={touchSx} />}

      {tab === "home" && today && (
        <Stack spacing={2}>
          <Alert severity={today.hasTimetable ? "success" : "info"}>
            {today.hasTimetable
              ? `Timetable-derived context — ${today.message}`
              : `Manually selected context — Course → Group → Semester → Subject → Period. Timetable is not required. ${today.message}`}
          </Alert>

          <Stack direction={{ xs: "column", md: "row" }} spacing={2}>
            <Box sx={{flex: 1}}>
              <Typography variant="h6">Current class</Typography>
              {today.currentClass ? classCard(today.currentClass) : <Typography color="text.secondary">No active class.</Typography>}
            </Box>
            <Box sx={{flex: 1}}>
              <Typography variant="h6">Next class</Typography>
              {today.nextClass ? classCard(today.nextClass) : <Typography color="text.secondary">No upcoming class.</Typography>}
            </Box>
          </Stack>

          <Box>
            <Typography variant="h6">Today&apos;s schedule</Typography>
            <Stack spacing={1} sx={{mt: 1}}>
              {schedule.length === 0 ? (
                <Typography color="text.secondary">No timetable classes today — use manual attendance.</Typography>
              ) : (
                schedule.map(classCard)
              )}
            </Stack>
          </Box>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <Box sx={{flex: 1}}>
              <Typography variant="subtitle1">Attendance summary</Typography>
              <Typography variant="body2">
                Taken {today.attendanceSummary.attendanceTaken} · Pending {today.attendanceSummary.pending} · Missed{" "}
                {today.attendanceSummary.missed}
              </Typography>
              <Typography variant="body2">
                Present {today.attendanceSummary.presentMarks} · Absent {today.attendanceSummary.absentMarks}
              </Typography>
            </Box>
            <Box sx={{flex: 1}}>
              <Typography variant="subtitle1">AI attendance</Typography>
              <Typography variant="body2">
                Sessions {today.aiAttendanceSummary.sessionsToday} · Pending reviews{" "}
                {today.aiAttendanceSummary.pendingReviews} · Accuracy{" "}
                {today.aiAttendanceSummary.averageRecognitionAccuracy?.toFixed(1) ?? "—"}%
              </Typography>
            </Box>
          </Stack>

          {today.pendingReviews.length > 0 && (
            <Box>
              <Typography variant="h6">Pending reviews</Typography>
              <Stack spacing={1} sx={{mt: 1}}>
                {today.pendingReviews.map((r) => (
                  <Button key={r.attendanceSessionId} variant="outlined" sx={touchSx} onClick={() => openReview(r.reviewPath)}>
                    {r.label} · {r.pendingCount} pending
                  </Button>
                ))}
              </Stack>
            </Box>
          )}

          {stickyActions}
        </Stack>
      )}

      {tab === "timeline" && (
        <FacultyTimelinePanel
          touchSx={touchSx}
          onNavigate={(path) => navigate(path)}
          swipeIndex={swipeIndex}
          onSwipeIndex={setSwipeIndex}
        />
      )}

      {tab === "calendar" && <FacultyCalendarPanel touchSx={touchSx} />}

      {tab === "productivity" && (
        <FacultyProductivityPanel touchSx={touchSx} onNavigate={(path) => navigate(path)} />
      )}

      {tab === "search" && <FacultySearchPanel touchSx={touchSx} onNavigate={(path) => navigate(path)} />}

      {tab === "navigation" && (
        <FacultyNavigationPanel
          roomId={roomIdParam ?? today?.currentClass?.roomId ?? undefined}
          fromRoomId={today?.currentClass?.roomId ?? undefined}
          touchSx={touchSx}
        />
      )}

      {tab === "preferences" && (
        <FacultyPreferencesPanel touchSx={touchSx} onPreferencesLoaded={setPrefs} />
      )}

      {tab === "class" && (
        <Stack spacing={2}>
          <Alert severity="info">{current?.message ?? "Loading current class…"}</Alert>
          {current?.currentClass ? (
            <>
              {classCard(current.currentClass)}
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                <Button variant="contained" sx={touchSx} onClick={() => openAttendance(false)}>
                  Take Attendance
                </Button>
                <Button variant="contained" color="secondary" sx={touchSx} onClick={() => openAttendance(true)}>
                  AI Attendance
                </Button>
                <Button variant="outlined" sx={touchSx} onClick={() => setTab("timetable")}>
                  View Timetable
                </Button>
                <Button variant="outlined" sx={touchSx} onClick={() => navigate("/attendance")}>
                  Student List
                </Button>
                {current.currentClass.attendanceSessionId && (
                  <Button
                    variant="outlined"
                    sx={touchSx}
                    onClick={() => navigate(`/attendance/sessions/${current.currentClass!.attendanceSessionId}/review`)}
                  >
                    Open Review
                  </Button>
                )}
              </Stack>
            </>
          ) : (
            <Typography>
              No active class. Use{" "}
              <Button component={RouterLink} to="/attendance?ai=0">
                manual attendance
              </Button>
              .
            </Typography>
          )}
          {stickyActions}
        </Stack>
      )}

      {tab === "timetable" && (
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
            {["Today", "Week", "Month", "Agenda"].map((v) => (
              <Button
                key={v}
                variant={timetableView === v ? "contained" : "outlined"}
                sx={touchSx}
                onClick={() => setTimetableView(v)}
              >
                {v}
              </Button>
            ))}
          </Stack>
          <Typography variant="body2" color="text.secondary">
            {timetable ? `${timetable.from} → ${timetable.to}` : "—"} · Reuses published scheduling data
          </Typography>
          <Stack spacing={1}>{(timetable?.classes ?? []).map(classCard)}</Stack>
        </Stack>
      )}

      {tab === "insights" && insights && (
        <Stack spacing={1.5}>
          <Typography variant="h6">Attendance insights</Typography>
          <Typography variant="body2">
            Taken {insights.attendanceTaken} · Pending {insights.pending} · Missed {insights.missed}
          </Typography>
          <Typography variant="body2">
            Avg completion {insights.averageCompletionMinutes?.toFixed(1) ?? "—"} min · AI usage {insights.aiUsage} ·
            Accuracy {insights.recognitionAccuracy?.toFixed(1) ?? "—"}%
          </Typography>
          <Typography variant="subtitle1">Weekly</Typography>
          <Typography variant="body2">
            Sessions {insights.weekly.sessions} · Completed {insights.weekly.completed} · AI {insights.weekly.aiSessions} ·
            Acc {insights.weekly.avgAccuracy?.toFixed(1) ?? "—"}%
          </Typography>
          <Typography variant="subtitle1">Monthly</Typography>
          <Typography variant="body2">
            Sessions {insights.monthly.sessions} · Completed {insights.monthly.completed} · AI {insights.monthly.aiSessions} ·
            Acc {insights.monthly.avgAccuracy?.toFixed(1) ?? "—"}%
          </Typography>
        </Stack>
      )}

      {tab === "notifications" && (
        <Stack spacing={2}>
          <FacultySmartNotificationsPanel />
          <Typography variant="h6">Schedule changes & substitutions</Typography>
          <Typography variant="caption" color="text.secondary">
            Live via SignalR — no polling
          </Typography>
          {notifications.length === 0 ? (
            <Typography color="text.secondary">No recent schedule notifications.</Typography>
          ) : (
            notifications.map((n) => (
              <Box key={n.notificationId} sx={{p: 1.5, border: "1px solid", borderColor: "divider", borderRadius: 2}}>
                <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                  <Chip size="small" label={n.kind} />
                  <Typography sx={{ fontWeight: 600 }}>{n.title}</Typography>
                </Stack>
                <Typography variant="body2">{n.message}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {new Date(n.occurredUtc).toLocaleString()}
                </Typography>
              </Box>
            ))
          )}
        </Stack>
      )}

      {!hasPermission("Attendance.Manage") && (
        <Alert severity="warning">Attendance.Manage permission is required for workspace actions.</Alert>
      )}

      <Dialog open={Boolean(autoResume?.shouldPrompt)} onClose={() => setAutoResume(null)}>
        <DialogTitle>Resume attendance?</DialogTitle>
        <DialogContent>
          <Typography>{autoResume?.message}</Typography>
          {autoResume?.session && (
            <Typography variant="body2" sx={{ mt: 1 }}>
              {autoResume.session.workflowStatusName} · Subject #{autoResume.session.subjectId}
            </Typography>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 1 }}>
            Never resumes automatically — you choose. Decision can be remembered in workspace preferences.
          </Typography>
        </DialogContent>
        <DialogActions sx={{ flexWrap: "wrap", gap: 1 }}>
          <Button
            variant="contained"
            onClick={() => {
              const path = autoResume?.session?.resumePath;
              void decideAutoResume("resume", autoResume?.session?.sessionId, true);
              setAutoResume(null);
              if (path) navigate(path);
            }}
          >
            Resume attendance
          </Button>
          <Button
            onClick={() => {
              const path = autoResume?.session?.resumePath;
              void decideAutoResume("continueReview", autoResume?.session?.sessionId, true);
              setAutoResume(null);
              if (path) navigate(path);
            }}
          >
            Continue review
          </Button>
          <Button
            onClick={() => {
              void decideAutoResume("dismiss", autoResume?.session?.sessionId, true);
              setAutoResume(null);
            }}
          >
            Dismiss
          </Button>
        </DialogActions>
      </Dialog>
    </Stack>
  );
};

export default FacultyWorkspacePage;
