/**
 * AI31.8.2 — lightweight layout token contracts (presentation only).
 */
import {
  denseKpiColumns,
  fluidDashboardSx,
  HERO_SUMMARY_CODES,
  sectionAccent,
  severityRank,
  standardKpiColumns,
  trendGlyph,
} from "./dashboardLayoutTokens";
import {
  OPERATIONAL_KPI_PRIORITY,
  REMOVED_EXECUTIVE_KPI_CODES,
  composeMorningBrief,
  composeOperationalExecutiveKpis,
  resolveActiveFilters,
} from "./executiveInformationArchitecture";
import type { EnterpriseDashboardExcellenceDto } from "../../services/enterpriseDashboardService";

const assert = (cond: boolean, msg: string) => {
  if (!cond) throw new Error(msg);
};

assert(fluidDashboardSx.maxWidth.xl === 1750, "1920+ max-width must be 1750");
assert((fluidDashboardSx as Record<string, string>)["--dash-card-sm"] === "112px", "Denser small card height");
assert((fluidDashboardSx as Record<string, string>)["--dash-context-max-h"] === "70px", "Context header max height");
assert(denseKpiColumns.xl === 4, "Hero summary uses 4 compact columns on xl");
assert(HERO_SUMMARY_CODES.length === 8, "Operational executive keeps eight KPIs");
assert(OPERATIONAL_KPI_PRIORITY[0] === "exec-critical-alerts", "Critical alerts first");
assert(REMOVED_EXECUTIVE_KPI_CODES.includes("exec-college"), "College removed from KPI cards");
assert(severityRank("Red") < severityRank("Orange"), "Critical sorts before high");
assert(standardKpiColumns.lg === 4, "Desktop KPI density is 4 columns");
assert(trendGlyph("up") === "▲", "Up trend glyph");
assert(Boolean(sectionAccent.attention.border), "Attention accent present");
assert(Boolean(sectionAccent.context.border), "Context accent present");

const chips = resolveActiveFilters(
  { campusId: 1, departmentId: 2 },
  {
    academicYears: [],
    departments: [{ id: 2, name: "CSE" }],
    courses: [],
    campuses: [{ id: 1, name: "Main" }],
    buildings: [],
    rooms: [],
  },
);
assert(chips.length === 2, "Only active filters resolve");
assert(chips[0].value === "Main", "Campus name resolved");

const stub = {
  executiveSummary: {
    todaysDate: "2026-07-22",
    currentWorkingDay: "Wednesday",
    criticalAlerts: 2,
    platformHealth: "Healthy",
    totalScheduledClassesToday: 10,
    overallAttendanceToday: "18.8%",
    cards: [
      { code: "exec-alerts", title: "Critical Alerts", value: 2, displayValue: "2", kind: "Kpi", category: "E", configurable: false, visible: true, sortOrder: 0 },
      { code: "exec-classes", title: "Classes", value: 10, displayValue: "10", kind: "Kpi", category: "E", configurable: false, visible: true, sortOrder: 1 },
      { code: "exec-health", title: "Health", displayValue: "Healthy", kind: "Kpi", category: "E", configurable: false, visible: true, sortOrder: 2 },
      { code: "exec-attendance", title: "Att", displayValue: "18.8%", kind: "Kpi", category: "E", configurable: false, visible: true, sortOrder: 3 },
    ],
  },
  commandCenter: {
    todaysOperations: {
      code: "today",
      title: "Today",
      cards: [
        { code: "classes-running-now", title: "Running", value: 4, displayValue: "4", kind: "Kpi", category: "T", configurable: false, visible: true, sortOrder: 0 },
        { code: "faculty-teaching-now", title: "Faculty", value: 4, displayValue: "4", kind: "Kpi", category: "T", configurable: false, visible: true, sortOrder: 1 },
        { code: "attendance-completion-today", title: "Completion", displayValue: "18.8%", value: 18.8, kind: "Kpi", category: "T", configurable: false, visible: true, sortOrder: 2 },
      ],
      quickLinks: [],
    },
    attendanceOperations: {
      code: "attendance",
      title: "Attendance",
      cards: [
        { code: "attendance-review-queue", title: "Reviews", value: 37, displayValue: "37", kind: "Kpi", category: "A", configurable: false, visible: true, sortOrder: 0 },
        { code: "completed-today", title: "Completed", value: 3, displayValue: "3", kind: "Kpi", category: "A", configurable: false, visible: true, sortOrder: 1 },
      ],
      quickLinks: [],
    },
    attentionRequired: { code: "attention", title: "Attention", cards: [], quickLinks: [] },
  },
} as unknown as EnterpriseDashboardExcellenceDto;

const kpis = composeOperationalExecutiveKpis(stub);
assert(kpis[0].code === "exec-critical-alerts", "Priority starts with critical alerts");
assert(kpis.length === 8, "Eight operational KPIs composed");
const brief = composeMorningBrief(kpis, stub.executiveSummary);
assert(brief.text.includes("10 scheduled"), "Morning brief includes scheduled classes");
assert(brief.text.includes("18.8%"), "Morning brief includes completion");
assert(brief.text.includes("37"), "Morning brief includes pending reviews");

export {};
