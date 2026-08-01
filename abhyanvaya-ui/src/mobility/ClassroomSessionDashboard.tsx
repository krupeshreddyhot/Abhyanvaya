import EventAvailableIcon from "@mui/icons-material/EventAvailable";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import ScheduleIcon from "@mui/icons-material/Schedule";
import {
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import type { AIStatus } from "../types/aiWorkflow";

export type ClassroomSessionCardModel = {
  id: string;
  title: string;
  subtitle?: string;
  periodLabel?: string;
  attendanceStatus?: string;
  recognitionStatus?: AIStatus | string;
  isCurrent?: boolean;
  isNext?: boolean;
};

export type ClassroomSessionDashboardProps = {
  facultyName?: string;
  todaysClassesCount?: number;
  current?: ClassroomSessionCardModel | null;
  next?: ClassroomSessionCardModel | null;
  recent?: ClassroomSessionCardModel[];
  onQuickStartAi?: () => void;
  quickStartDisabled?: boolean;
};

/**
 * AI22.7C Phase 1.4 — mobile-first classroom session dashboard.
 * Reuses existing attendance context / faculty info — no new APIs.
 */
export function ClassroomSessionDashboard({
  facultyName,
  todaysClassesCount = 0,
  current = null,
  next = null,
  recent = [],
  onQuickStartAi,
  quickStartDisabled = false,
}: ClassroomSessionDashboardProps) {
  return (
    <Stack spacing={1.5} aria-label="Classroom session dashboard" data-mobility="classroom-dashboard">
      <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
        <Box>
          <Typography variant="h6" sx={{ fontWeight: 700 }}>
            Today’s classroom
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {facultyName ? `${facultyName} · ` : ""}
            {todaysClassesCount} class{todaysClassesCount === 1 ? "" : "es"}
          </Typography>
        </Box>
        {onQuickStartAi ? (
          <Button
            variant="contained"
            size="large"
            startIcon={<PlayArrowIcon />}
            disabled={quickStartDisabled}
            onClick={onQuickStartAi}
            sx={{ minHeight: 48, whiteSpace: "nowrap" }}
          >
            Quick Start AI
          </Button>
        ) : null}
      </Stack>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.25}>
        <SessionCard
          emptyLabel="No class in progress"
          icon={<EventAvailableIcon color="primary" />}
          model={current}
          badge="Current"
          color="primary"
        />
        <SessionCard
          emptyLabel="No upcoming class"
          icon={<ScheduleIcon color="secondary" />}
          model={next}
          badge="Next"
          color="secondary"
        />
      </Stack>

      {recent.length > 0 ? (
        <Stack spacing={1}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
            Recent sessions
          </Typography>
          {recent.map((item) => (
            <Card key={item.id} variant="outlined">
              <CardContent sx={{ py: 1.25, "&:last-child": { pb: 1.25 } }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                  {item.title}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {item.subtitle}
                  {item.recognitionStatus ? ` · ${item.recognitionStatus}` : ""}
                </Typography>
              </CardContent>
            </Card>
          ))}
        </Stack>
      ) : null}
    </Stack>
  );
}

function SessionCard({
  model,
  badge,
  color,
  icon,
  emptyLabel,
}: {
  model: ClassroomSessionCardModel | null;
  badge: string;
  color: "primary" | "secondary";
  icon: React.ReactNode;
  emptyLabel: string;
}) {
  return (
    <Card variant="outlined" sx={{ flex: 1, minHeight: 120 }}>
      <CardActionArea disabled sx={{ height: "100%" }}>
        <CardContent>
          <Stack spacing={1}>
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              {icon}
              <Chip size="small" color={color} label={badge} />
            </Stack>
            {model ? (
              <>
                <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
                  {model.title}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {model.subtitle}
                  {model.periodLabel ? ` · ${model.periodLabel}` : ""}
                </Typography>
                <Stack direction="row" spacing={0.75} sx={{ flexWrap: "wrap", gap: 0.5 }}>
                  {model.attendanceStatus ? (
                    <Chip size="small" variant="outlined" label={model.attendanceStatus} />
                  ) : null}
                  {model.recognitionStatus ? (
                    <Chip size="small" variant="outlined" label={String(model.recognitionStatus)} />
                  ) : null}
                </Stack>
              </>
            ) : (
              <Typography variant="body2" color="text.secondary">
                {emptyLabel}
              </Typography>
            )}
          </Stack>
        </CardContent>
      </CardActionArea>
    </Card>
  );
}

export default ClassroomSessionDashboard;
