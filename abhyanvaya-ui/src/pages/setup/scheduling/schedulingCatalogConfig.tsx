import type { ReactNode } from "react";
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
import GavelIcon from "@mui/icons-material/Gavel";
import HistoryIcon from "@mui/icons-material/History";
import LayersIcon from "@mui/icons-material/Layers";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import PublishIcon from "@mui/icons-material/Publish";
import PolicyIcon from "@mui/icons-material/Policy";
import ReportProblemIcon from "@mui/icons-material/ReportProblem";
import MapIcon from "@mui/icons-material/Map";

/** AI30.3.5.1 — dependency-ordered scheduling catalog. Paths must match existing routes. */
export type SchedulingHubLink = {
  key: string;
  to: string;
  title: string;
  description: string;
  icon: ReactNode;
  helpDocPath: string;
};

export type SchedulingHubGroup = {
  id: string;
  title: string;
  items: SchedulingHubLink[];
};

export const schedulingHubGroups: SchedulingHubGroup[] = [
  {
    id: "academic-calendar",
    title: "Academic Calendar",
    items: [
      {
        key: "academic-years",
        to: "/setup/scheduling/academic-years",
        title: "Academic Years",
        description: "Define years, set current, clone previous",
        icon: <CalendarMonthIcon />,
        helpDocPath: "/docs/scheduling/modules/academic-years.md",
      },
      {
        key: "working-days",
        to: "/setup/scheduling/working-days",
        title: "Working Days",
        description: "Toggle Mon–Sun working days per academic year",
        icon: <TodayIcon />,
        helpDocPath: "/docs/scheduling/modules/working-days.md",
      },
      {
        key: "holidays",
        to: "/setup/scheduling/holidays",
        title: "Holiday Calendar",
        description: "National, university, college, and exam holidays",
        icon: <EventBusyIcon />,
        helpDocPath: "/docs/scheduling/modules/holidays.md",
      },
      {
        key: "holiday-types",
        to: "/setup/scheduling/holiday-types",
        title: "Holiday Types",
        description: "Holiday type catalog with colour and priority",
        icon: <PaletteIcon />,
        helpDocPath: "/docs/scheduling/modules/holiday-types.md",
      },
    ],
  },
  {
    id: "infrastructure",
    title: "Campus & Infrastructure",
    items: [
      {
        key: "campuses",
        to: "/setup/scheduling/campuses",
        title: "Campus Facilities",
        description: "Campuses, buildings, and floors",
        icon: <BusinessIcon />,
        helpDocPath: "/docs/scheduling/modules/campuses.md",
      },
      {
        key: "rooms",
        to: "/setup/scheduling/rooms",
        title: "Rooms",
        description: "Search, filter, and manage rooms with features",
        icon: <MeetingRoomIcon />,
        helpDocPath: "/docs/scheduling/modules/rooms.md",
      },
      {
        key: "room-features",
        to: "/setup/scheduling/room-features",
        title: "Room Features",
        description: "Feature catalog and per-room assignments",
        icon: <ExtensionIcon />,
        helpDocPath: "/docs/scheduling/modules/room-features.md",
      },
      {
        key: "room-availability",
        to: "/setup/scheduling/room-availability",
        title: "Room Availability",
        description: "Maintenance, blocked, and reserved room windows",
        icon: <EventBusyIcon />,
        helpDocPath: "/docs/scheduling/modules/room-availability.md",
      },
    ],
  },
  {
    id: "framework",
    title: "Scheduling Framework",
    items: [
      {
        key: "time-slots",
        to: "/setup/scheduling/time-slots",
        title: "Time Slots",
        description: "Period sets, breaks, lunch, and working sessions",
        icon: <ScheduleIcon />,
        helpDocPath: "/docs/scheduling/modules/time-slots.md",
      },
      {
        key: "time-slot-templates",
        to: "/setup/scheduling/time-slot-templates",
        title: "Time Slot Templates",
        description: "Reusable templates with clone, preview, and default",
        icon: <ViewTimelineIcon />,
        helpDocPath: "/docs/scheduling/modules/time-slot-templates.md",
      },
      {
        key: "subject-categories",
        to: "/setup/scheduling/subject-categories",
        title: "Subject Categories",
        description: "Category catalog and subject scheduling fields",
        icon: <CategoryIcon />,
        helpDocPath: "/docs/scheduling/modules/subject-categories.md",
      },
      {
        key: "subject-delivery",
        to: "/setup/scheduling/subject-delivery",
        title: "Subject Delivery",
        description: "Delivery types and subject delivery requirements",
        icon: <LocalShippingIcon />,
        helpDocPath: "/docs/scheduling/modules/subject-delivery.md",
      },
      {
        key: "room-rules",
        to: "/setup/scheduling/room-rules",
        title: "Room Rules",
        description: "Room allocation preference rules",
        icon: <RuleIcon />,
        helpDocPath: "/docs/scheduling/modules/room-rules.md",
      },
    ],
  },
  {
    id: "faculty-planning",
    title: "Faculty Planning",
    items: [
      {
        key: "faculty-availability",
        to: "/setup/scheduling/faculty-availability",
        title: "Faculty Availability",
        description: "Weekly, monthly, and timeline faculty availability",
        icon: <EventAvailableIcon />,
        helpDocPath: "/docs/scheduling/modules/faculty-availability.md",
      },
      {
        key: "faculty-preferences",
        to: "/setup/scheduling/faculty-preferences",
        title: "Faculty Preferences",
        description: "Teaching mode, location, subject, and time preferences",
        icon: <TuneIcon />,
        helpDocPath: "/docs/scheduling/modules/faculty-preferences.md",
      },
      {
        key: "faculty-workloads",
        to: "/setup/scheduling/faculty-workloads",
        title: "Faculty Workloads",
        description: "Max periods, loads, guest/adjunct, day preferences",
        icon: <PersonIcon />,
        helpDocPath: "/docs/scheduling/modules/faculty-workloads.md",
      },
      {
        key: "subject-allocations",
        to: "/setup/scheduling/subject-allocations",
        title: "Subject Allocation",
        description: "Staff–subject assignments with weekly hours (Catalog departments)",
        icon: <MenuBookIcon />,
        helpDocPath: "/docs/scheduling/modules/subject-allocations.md",
      },
    ],
  },
  {
    id: "timetable",
    title: "Timetable Design",
    items: [
      {
        key: "schedule-versions",
        to: "/setup/scheduling/governance/versions",
        title: "Schedule Versions",
        description: "Create, duplicate, clone previous, mark current, and archive versions",
        icon: <LayersIcon />,
        helpDocPath: "/docs/scheduling/modules/schedule-versions.md",
      },
      {
        key: "timetable-designer",
        to: "/setup/scheduling/timetables",
        title: "Timetable Designer",
        description: "Create and edit draft timetables with drag-and-drop scheduling",
        icon: <GridOnIcon />,
        helpDocPath: "/docs/scheduling/modules/timetable-designer.md",
      },
      {
        key: "faculty-timetable",
        to: "/setup/scheduling/timetable-faculty",
        title: "Faculty Timetable",
        description: "Read-only faculty weekly or daily view with print and Excel export",
        icon: <PersonSearchIcon />,
        helpDocPath: "/docs/scheduling/modules/faculty-timetable.md",
      },
      {
        key: "student-timetable",
        to: "/setup/scheduling/timetable-student",
        title: "Student Timetable",
        description: "Course, group, and semester timetable with print and Excel export",
        icon: <SchoolIcon />,
        helpDocPath: "/docs/scheduling/modules/student-timetable.md",
      },
      {
        key: "room-timetable",
        to: "/setup/scheduling/timetable-room",
        title: "Room Timetable",
        description: "Room occupancy weekly or daily view with print and Excel export",
        icon: <MeetingRoomOutlinedIcon />,
        helpDocPath: "/docs/scheduling/modules/room-timetable.md",
      },
    ],
  },
  {
    id: "governance",
    title: "Governance",
    items: [
      {
        key: "approval-queue",
        to: "/setup/scheduling/governance/approvals",
        title: "Approval Queue",
        description: "Review timetables with timeline, comments, and approve/reject/return",
        icon: <GavelIcon />,
        helpDocPath: "/docs/scheduling/modules/approval-queue.md",
      },
      {
        key: "publishing",
        to: "/setup/scheduling/governance/publishing",
        title: "Publishing",
        description: "Publish and archive timetables with lifecycle timeline",
        icon: <PublishIcon />,
        helpDocPath: "/docs/scheduling/modules/publishing.md",
      },
      {
        key: "clone-wizard",
        to: "/setup/scheduling/governance/clone",
        title: "Clone Wizard",
        description: "Clone timetables by day, week, department, faculty, or room",
        icon: <ContentCopyIcon />,
        helpDocPath: "/docs/scheduling/modules/clone-wizard.md",
      },
      {
        key: "change-history",
        to: "/setup/scheduling/governance/history",
        title: "Change History",
        description: "Filter timetable change timeline and export to Excel",
        icon: <HistoryIcon />,
        helpDocPath: "/docs/scheduling/modules/change-history.md",
      },
      {
        key: "governance-dashboard",
        to: "/setup/scheduling/governance/dashboard",
        title: "Governance Dashboard",
        description: "Approval trends, version growth, publishing history, and governance KPIs",
        icon: <PolicyIcon />,
        helpDocPath: "/docs/scheduling/modules/governance-dashboard.md",
      },
    ],
  },
  {
    id: "validation",
    title: "Validation",
    items: [
      {
        key: "conflict-dashboard",
        to: "/setup/scheduling/conflicts/dashboard",
        title: "Conflict Dashboard",
        description: "Conflict counts, categories, warning trends, and heat maps",
        icon: <MapIcon />,
        helpDocPath: "/docs/scheduling/modules/conflict-dashboard.md",
      },
      {
        key: "conflict-workspace",
        to: "/setup/scheduling/conflicts/workspace",
        title: "Conflict Workspace",
        description: "Explain, guide, pin, and navigate conflicts — advisory only",
        icon: <ReportProblemIcon />,
        helpDocPath: "/docs/scheduling/modules/conflict-workspace.md",
      },
      {
        key: "conflict-analytics",
        to: "/setup/scheduling/conflicts/analytics",
        title: "Conflict Analytics",
        description: "Historical conflict trends, resolution rate, Excel/PDF export",
        icon: <MapIcon />,
        helpDocPath: "/docs/scheduling/modules/conflict-analytics.md",
      },
      {
        key: "conflict-rules",
        to: "/setup/scheduling/conflicts/rules",
        title: "Conflict Rule Thresholds",
        description: "Configure detection thresholds with audit history",
        icon: <ReportProblemIcon />,
        helpDocPath: "/docs/scheduling/modules/conflict-rules.md",
      },
    ],
  },
  {
    id: "optimization",
    title: "Optimization",
    items: [
      {
        key: "optimization-preview",
        to: "/setup/scheduling/optimization/preview",
        title: "Optimization Preview",
        description: "Readiness preview — score, metrics, conflicts (no apply)",
        icon: <MapIcon />,
        helpDocPath: "/docs/scheduling/modules/optimization-preview.md",
      },
      {
        key: "optimization-workspace",
        to: "/setup/scheduling/optimization/workspace",
        title: "Optimization Workspace",
        description: "Sandbox scenarios — save, replay, compare, collaborate (no apply)",
        icon: <MapIcon />,
        helpDocPath: "/docs/scheduling/modules/optimization-workspace.md",
      },
      {
        key: "optimization-dashboard",
        to: "/setup/scheduling/optimization/dashboard",
        title: "Optimization Dashboard",
        description: "Run enterprise pipeline, review comparison, approve new draft versions",
        icon: <DashboardIcon />,
        helpDocPath: "/docs/scheduling/modules/optimization-dashboard.md",
      },
    ],
  },
];

export const schedulingDashboardLink: SchedulingHubLink = {
  key: "dashboard",
  to: "/setup/scheduling/dashboard",
  title: "Dashboard",
  description: "Overview counts and configuration readiness",
  icon: <DashboardIcon />,
  helpDocPath: "/docs/scheduling/configuration-guide.md",
};
