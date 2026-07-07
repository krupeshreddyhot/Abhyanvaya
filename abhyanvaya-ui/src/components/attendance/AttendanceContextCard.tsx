import CalendarMonthIcon from "@mui/icons-material/CalendarMonth";
import ClassIcon from "@mui/icons-material/Class";
import GroupsIcon from "@mui/icons-material/Groups";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import ScheduleIcon from "@mui/icons-material/Schedule";
import SchoolIcon from "@mui/icons-material/School";
import { Box, Grid, Paper, Stack, Typography } from "@mui/material";
import type { ReactNode } from "react";
import type { AttendanceContext } from "../../types/attendanceContext";

const formatAttendanceDate = (date: string): string => {
  if (!date) return "—";
  const parsed = new Date(`${date}T00:00:00`);
  if (Number.isNaN(parsed.getTime())) return "—";
  return parsed.toLocaleDateString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
  });
};

type ContextFieldProps = {
  icon: ReactNode;
  label: string;
  value: string;
};

const ContextField = ({ icon, label, value }: ContextFieldProps) => (
  <Grid container spacing={1} sx={{ alignItems: "center" }}>
    <Grid size={{ xs: 5, sm: 4 }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", minWidth: 0 }}>
        <Box sx={{ color: "primary.main", display: "flex", flexShrink: 0 }}>{icon}</Box>
        <Typography variant="body2" color="text.secondary" noWrap>
          {label}
        </Typography>
      </Stack>
    </Grid>
    <Grid size={{ xs: 7, sm: 8 }}>
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {value}
      </Typography>
    </Grid>
  </Grid>
);

export type AttendanceContextCardProps = {
  context: AttendanceContext;
};

export const AttendanceContextCard = ({ context }: AttendanceContextCardProps) => {
  const courseLabel = context.courseName ?? (context.courseId > 0 ? `Course #${context.courseId}` : "—");
  const groupLabel = context.groupName ?? (context.groupId > 0 ? `Group #${context.groupId}` : "—");
  const semesterLabel =
    context.semesterName ?? (context.semesterId > 0 ? `Semester #${context.semesterId}` : "—");
  const subjectLabel =
    context.subjectName ?? (context.subjectId > 0 ? `Subject #${context.subjectId}` : "—");

  const contextFields: ContextFieldProps[] = [
    { icon: <SchoolIcon fontSize="small" />, label: "Course", value: courseLabel },
    { icon: <GroupsIcon fontSize="small" />, label: "Group", value: groupLabel },
    { icon: <ClassIcon fontSize="small" />, label: "Semester", value: semesterLabel },
    { icon: <MenuBookIcon fontSize="small" />, label: "Subject", value: subjectLabel },
    {
      icon: <ScheduleIcon fontSize="small" />,
      label: "Period",
      value: `Period ${context.periodNumber}`,
    },
    {
      icon: <CalendarMonthIcon fontSize="small" />,
      label: "Attendance Date",
      value: formatAttendanceDate(context.attendanceDate),
    },
  ];

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Typography variant="subtitle2" gutterBottom>
        Attendance Context
      </Typography>
      <Grid container spacing={2}>
        {contextFields.map((field) => (
          <Grid key={field.label} size={{ xs: 12, md: 6 }}>
            <ContextField {...field} />
          </Grid>
        ))}
      </Grid>
    </Paper>
  );
};

export default AttendanceContextCard;
