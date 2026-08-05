import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Typography,
} from "@mui/material";
import * as signalR from "@microsoft/signalr";
import {
  getEnterpriseNotifications,
  updateNotificationState,
  type EnterpriseNotificationCenterDto,
} from "../../services/enterpriseDashboardService";

/** AI31.6.7 — Enterprise Notification Center (SignalR, no polling). */
const EnterpriseNotificationCenterPage = () => {
  const [data, setData] = useState<EnterpriseNotificationCenterDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await getEnterpriseNotifications();
      setData(res.data);
    } catch {
      setError("Unable to load notifications.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) return;
    const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/api\/?$/, "") ?? "";
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/faculty`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();
    const refresh = () => void load();
    connection.on("FacultyScheduleNotification", refresh);
    connection.on("AttendanceRecoveryNotification", refresh);
    void connection.start().catch(() => undefined);
    return () => {
      void connection.stop();
    };
  }, []);

  const mutate = async (
    notificationId: string,
    patch: Omit<Parameters<typeof updateNotificationState>[0], "notificationId">,
  ) => {
    const res = await updateNotificationState({ ...patch, notificationId });
    setData(res.data);
  };

  if (loading && !data) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Notification Center
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Unread: {data?.unreadCount ?? 0} · Sources: Scheduling, Attendance, Recovery, System · SignalR only
      </Typography>
      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      <Stack spacing={1.5}>
        {(data?.items ?? []).map((n) => (
          <Paper key={n.notificationId} sx={{ p: 2, opacity: n.isArchived ? 0.6 : 1 }}>
            <Stack
              direction={{ xs: "column", sm: "row" }}
              spacing={1}
              sx={{ justifyContent: "space-between" }}
            >
              <Box>
                <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", mb: 0.5 }}>
                  <Chip size="small" label={n.source} />
                  <Chip
                    size="small"
                    label={n.priority}
                    color={n.priority === "Critical" || n.priority === "High" ? "warning" : "default"}
                  />
                  <Chip size="small" label={n.category} variant="outlined" />
                  {n.isPinned && <Chip size="small" color="primary" label="Pinned" />}
                  {n.isUnread && <Chip size="small" color="error" label="Unread" />}
                </Stack>
                <Typography sx={{ fontWeight: 600 }}>{n.title}</Typography>
                <Typography variant="body2">{n.message}</Typography>
                <Typography variant="caption" color="text.secondary">
                  {new Date(n.occurredUtc).toLocaleString()}
                </Typography>
              </Box>
              <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap" }}>
                {n.isUnread && (
                  <Button size="small" onClick={() => void mutate(n.notificationId, { isRead: true })}>
                    Mark read
                  </Button>
                )}
                <Button size="small" onClick={() => void mutate(n.notificationId, { isPinned: !n.isPinned })}>
                  {n.isPinned ? "Unpin" : "Pin"}
                </Button>
                <Button size="small" onClick={() => void mutate(n.notificationId, { isArchived: true })}>
                  Archive
                </Button>
                <Button
                  size="small"
                  color="inherit"
                  onClick={() => void mutate(n.notificationId, { isDismissed: true })}
                >
                  Dismiss
                </Button>
              </Stack>
            </Stack>
          </Paper>
        ))}
      </Stack>
    </Box>
  );
};

export default EnterpriseNotificationCenterPage;
