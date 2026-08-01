import { Link as RouterLink } from "react-router-dom";
import { Box, Button, Card, CardActionArea, CardContent, Stack, Typography } from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import CalendarMonthIcon from "@mui/icons-material/CalendarMonth";
import DashboardIcon from "@mui/icons-material/Dashboard";
import CategoryIcon from "@mui/icons-material/Category";
import EventAvailableIcon from "@mui/icons-material/EventAvailable";
import EventBusyIcon from "@mui/icons-material/EventBusy";
import ViewTimelineIcon from "@mui/icons-material/ViewTimeline";
import BusinessIcon from "@mui/icons-material/Business";
import MeetingRoomIcon from "@mui/icons-material/MeetingRoom";
import ScheduleIcon from "@mui/icons-material/Schedule";
import PersonIcon from "@mui/icons-material/Person";
import MenuBookIcon from "@mui/icons-material/MenuBook";
import RuleIcon from "@mui/icons-material/Rule";
import TodayIcon from "@mui/icons-material/Today";
import TuneIcon from "@mui/icons-material/Tune";
import ExtensionIcon from "@mui/icons-material/Extension";
import LocalShippingIcon from "@mui/icons-material/LocalShipping";
import PaletteIcon from "@mui/icons-material/Palette";
import GridOnIcon from "@mui/icons-material/GridOn";
import PersonSearchIcon from "@mui/icons-material/PersonSearch";
import SchoolIcon from "@mui/icons-material/School";
import MeetingRoomOutlinedIcon from "@mui/icons-material/MeetingRoomOutlined";
import AnalyticsIcon from "@mui/icons-material/Analytics";
import GavelIcon from "@mui/icons-material/Gavel";
import HistoryIcon from "@mui/icons-material/History";
import LayersIcon from "@mui/icons-material/Layers";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import PublishIcon from "@mui/icons-material/Publish";
import PolicyIcon from "@mui/icons-material/Policy";

type HubLink = {
  to: string;
  title: string;
  description: string;
  icon: React.ReactNode;
};

/** AC1 ordered foundation links — Catalog owns Departments (not listed here). */
const links: HubLink[] = [
  {
    to: "/setup/scheduling/dashboard",
    title: "Dashboard",
    description: "Overview counts for scheduling foundation data",
    icon: <DashboardIcon />,
  },
  {
    to: "/setup/scheduling/academic-years",
    title: "Academic years",
    description: "Define years, set current, clone previous",
    icon: <CalendarMonthIcon />,
  },
  {
    to: "/setup/scheduling/working-days",
    title: "Working days",
    description: "Toggle Mon–Sun working days per academic year",
    icon: <TodayIcon />,
  },
  {
    to: "/setup/scheduling/holidays",
    title: "Holiday calendar",
    description: "National, university, college, and exam holidays",
    icon: <EventBusyIcon />,
  },
  {
    to: "/setup/scheduling/campuses",
    title: "Campus facilities",
    description: "Campuses, buildings, and floors",
    icon: <BusinessIcon />,
  },
  {
    to: "/setup/scheduling/rooms",
    title: "Rooms",
    description: "Search, filter, and manage rooms with features",
    icon: <MeetingRoomIcon />,
  },
  {
    to: "/setup/scheduling/time-slots",
    title: "Time slots",
    description: "Period sets, breaks, lunch, and working sessions",
    icon: <ScheduleIcon />,
  },
  {
    to: "/setup/scheduling/faculty-availability",
    title: "Faculty availability",
    description: "Weekly, monthly, and timeline faculty availability",
    icon: <EventAvailableIcon />,
  },
  {
    to: "/setup/scheduling/faculty-preferences",
    title: "Faculty preferences",
    description: "Teaching mode, location, subject, and time preferences",
    icon: <TuneIcon />,
  },
  {
    to: "/setup/scheduling/subject-allocations",
    title: "Subject allocation",
    description: "Staff–subject assignments with weekly hours (uses Catalog departments)",
    icon: <MenuBookIcon />,
  },
  {
    to: "/setup/scheduling/timetables",
    title: "Timetable designer",
    description: "Create and edit draft timetables with drag-and-drop scheduling",
    icon: <GridOnIcon />,
  },
  {
    to: "/setup/scheduling/room-features",
    title: "Room features",
    description: "Feature catalog and per-room assignments",
    icon: <ExtensionIcon />,
  },
  {
    to: "/setup/scheduling/subject-categories",
    title: "Subject categories",
    description: "Category catalog and subject scheduling fields",
    icon: <CategoryIcon />,
  },
  {
    to: "/setup/scheduling/subject-delivery",
    title: "Subject delivery",
    description: "Delivery types and subject delivery requirements",
    icon: <LocalShippingIcon />,
  },
  {
    to: "/setup/scheduling/holiday-types",
    title: "Holiday types",
    description: "Holiday type catalog with colour and priority",
    icon: <PaletteIcon />,
  },
  {
    to: "/setup/scheduling/time-slot-templates",
    title: "Time slot templates",
    description: "Reusable templates with clone, preview, and default",
    icon: <ViewTimelineIcon />,
  },
  {
    to: "/setup/scheduling/room-availability",
    title: "Room availability",
    description: "Maintenance, blocked, and reserved room windows",
    icon: <EventBusyIcon />,
  },
  {
    to: "/setup/scheduling/faculty-workloads",
    title: "Faculty workloads",
    description: "Max periods, loads, guest/adjunct, day preferences",
    icon: <PersonIcon />,
  },
  {
    to: "/setup/scheduling/room-rules",
    title: "Room rules",
    description: "Room allocation preference rules",
    icon: <RuleIcon />,
  },
  {
    to: "/setup/scheduling/timetable-faculty",
    title: "Faculty timetable",
    description: "Read-only faculty weekly or daily view with print and Excel export",
    icon: <PersonSearchIcon />,
  },
  {
    to: "/setup/scheduling/timetable-student",
    title: "Student timetable",
    description: "Course, group, and semester timetable with print and Excel export",
    icon: <SchoolIcon />,
  },
  {
    to: "/setup/scheduling/timetable-room",
    title: "Room timetable",
    description: "Room occupancy weekly or daily view with print and Excel export",
    icon: <MeetingRoomOutlinedIcon />,
  },
  {
    to: "/setup/scheduling/timetable-dashboard",
    title: "Timetable dashboard",
    description: "Draft/locked counts, daily distribution, faculty load, and room usage",
    icon: <AnalyticsIcon />,
  },
  {
    to: "/setup/scheduling/governance/dashboard",
    title: "Governance dashboard",
    description: "Approval trends, version growth, publishing history, and governance KPIs",
    icon: <PolicyIcon />,
  },
  {
    to: "/setup/scheduling/governance/versions",
    title: "Schedule versions",
    description: "Create, duplicate, clone previous, mark current, and archive versions",
    icon: <LayersIcon />,
  },
  {
    to: "/setup/scheduling/governance/approvals",
    title: "Approval queue",
    description: "Review timetables with timeline, comments, and approve/reject/return",
    icon: <GavelIcon />,
  },
  {
    to: "/setup/scheduling/governance/publishing",
    title: "Publishing",
    description: "Publish and archive timetables with lifecycle timeline",
    icon: <PublishIcon />,
  },
  {
    to: "/setup/scheduling/governance/clone",
    title: "Clone wizard",
    description: "Clone timetables by day, week, department, faculty, or room",
    icon: <ContentCopyIcon />,
  },
  {
    to: "/setup/scheduling/governance/history",
    title: "Change history",
    description: "Filter timetable change timeline and export to Excel",
    icon: <HistoryIcon />,
  },
];

const SchedulingHub = () => (
  <Stack spacing={3}>
    <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
      <Button component={RouterLink} to="/setup" startIcon={<ArrowBackIcon />} variant="text">
        Catalog
      </Button>
      <Typography variant="h4" sx={{ flexGrow: 1 }}>
        Scheduling
      </Typography>
    </Box>

    <Typography variant="body1" color="text.secondary">
      Enterprise scheduling foundation — academic calendar, facilities, time slots, faculty workloads, and allocation
      rules. Departments are maintained under Catalog → Departments (single source of truth).
    </Typography>

    <Box
      sx={{
        display: "grid",
        gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" },
        gap: 2,
      }}
    >
      {links.map((x) => (
        <Card key={x.to} variant="outlined">
          <CardActionArea component={RouterLink} to={x.to}>
            <CardContent>
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5 }}>
                {x.icon}
                <Typography variant="h6">{x.title}</Typography>
              </Box>
              <Typography variant="body2" color="text.secondary">
                {x.description}
              </Typography>
            </CardContent>
          </CardActionArea>
        </Card>
      ))}
    </Box>
  </Stack>
);

export default SchedulingHub;
