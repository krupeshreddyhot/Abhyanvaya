import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Chip,
  Stack,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import Timeline from "@mui/lab/Timeline";
import TimelineItem from "@mui/lab/TimelineItem";
import TimelineSeparator from "@mui/lab/TimelineSeparator";
import TimelineConnector from "@mui/lab/TimelineConnector";
import TimelineContent from "@mui/lab/TimelineContent";
import TimelineDot from "@mui/lab/TimelineDot";
import TimelineOppositeContent from "@mui/lab/TimelineOppositeContent";
import type { BatchProgressDto } from "../../types/enrollment";
import { buildTimelineEvents } from "../../utils/enrollmentStageUtils";

type Props = {
  progress?: BatchProgressDto;
  batch: {
    createdUtc: string;
    startedUtc: string | null;
    completedUtc: string | null;
    totalStudents: number;
    failedCount: number;
  };
};

const severityColor = (severity: "info" | "warning" | "error" | "success") => {
  switch (severity) {
    case "success":
      return "success";
    case "warning":
      return "warning";
    case "error":
      return "error";
    default:
      return "grey";
  }
};

const formatTimestamp = (ts: string | null) => {
  if (!ts) return "In progress";
  return new Date(ts).toLocaleString(undefined, { dateStyle: "short", timeStyle: "medium" });
};

const EnrollmentTimeline = ({ progress, batch }: Props) => {
  const events = buildTimelineEvents(progress, batch);

  return (
    <Accordion defaultExpanded disableGutters variant="outlined">
      <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls="enrollment-timeline-content">
        <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
          Enrollment Timeline
        </Typography>
      </AccordionSummary>
      <AccordionDetails>
        <Timeline position="alternate" sx={{ p: 0, m: 0 }}>
          {events.map((event, index) => (
            <TimelineItem key={event.id}>
              <TimelineOppositeContent color="text.secondary" sx={{ flex: 0.3 }}>
                <Typography variant="caption">{formatTimestamp(event.timestamp)}</Typography>
              </TimelineOppositeContent>
              <TimelineSeparator>
                <TimelineDot color={severityColor(event.severity) as "success" | "warning" | "error" | "grey"} />
                {index < events.length - 1 ? <TimelineConnector /> : null}
              </TimelineSeparator>
              <TimelineContent>
                <Stack spacing={0.5}>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {event.title}
                  </Typography>
                  {event.studentCount != null ? (
                    <Typography variant="caption" color="text.secondary">
                      Students: {event.studentCount}
                    </Typography>
                  ) : null}
                  {event.detail ? (
                    <Chip size="small" label={event.detail} color={event.severity === "error" ? "error" : "warning"} />
                  ) : null}
                </Stack>
              </TimelineContent>
            </TimelineItem>
          ))}
        </Timeline>
      </AccordionDetails>
    </Accordion>
  );
};

export default EnrollmentTimeline;
