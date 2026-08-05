/**
 * AI31.8.2 — Executive Dashboard Information Architecture (presentation/composition only).
 * Composes context, operational KPIs, and morning brief from existing excellence DTOs.
 * Does not call new APIs or alter attendance/scheduling engines.
 */
import type {
  DashboardFilterRequest,
  DashboardFilterStateDto,
  DashboardWidgetDto,
  EnterpriseDashboardExcellenceDto,
  ExecutiveSummaryDto,
} from "../../services/enterpriseDashboardService";

export type ActiveFilterChip = { key: string; label: string; value: string };

export type MorningBriefModel = {
  text: string;
  generatedAt: Date;
};

/** Operational Executive Summary priority (Prompt 4). */
export const OPERATIONAL_KPI_PRIORITY = [
  "exec-critical-alerts",
  "exec-classes-running",
  "exec-attendance-completion",
  "exec-pending-reviews",
  "exec-faculty-teaching",
  "exec-scheduled-classes",
  "exec-platform-health",
  "exec-attendance-recorded",
] as const;

/** Static / identity KPIs removed from Executive Summary (Prompt 7). */
export const REMOVED_EXECUTIVE_KPI_CODES = [
  "exec-college",
  "exec-academic-year",
  "exec-semester",
  "exec-working-day",
  "exec-date",
  "exec-students",
  "exec-faculty",
] as const;

const findCard = (cards: DashboardWidgetDto[] | undefined, ...codes: string[]) =>
  cards?.find((c) => codes.includes(c.code));

const cloneAs = (
  source: DashboardWidgetDto | undefined,
  code: string,
  title: string,
  subtitle: string,
  fallbackDisplay?: string,
): DashboardWidgetDto => {
  if (!source) {
    return {
      code,
      title,
      kind: "Kpi",
      category: "Executive",
      displayValue: fallbackDisplay ?? "—",
      value: null,
      status: "Info",
      statusLabel: undefined,
      path: "/dashboard",
      tooltip: subtitle,
      explanation: subtitle,
      configurable: false,
      visible: true,
      sortOrder: 0,
    };
  }
  return {
    ...source,
    code,
    title,
    explanation: subtitle,
    tooltip: source.tooltip ?? subtitle,
    category: "Executive",
    kind: "Kpi",
    visible: true,
  };
};

/** Resolve named filter options for the compact filter context panel. */
export const resolveActiveFilters = (
  filters: DashboardFilterRequest,
  options?: DashboardFilterStateDto | null,
): ActiveFilterChip[] => {
  const nameOf = (list: { id: number; name: string }[] | undefined, id?: number | null) =>
    id != null ? list?.find((x) => x.id === id)?.name ?? String(id) : null;

  const chips: ActiveFilterChip[] = [];
  const year = nameOf(options?.academicYears, filters.academicYearId);
  if (year) chips.push({ key: "academicYearId", label: "Academic Year", value: year });
  const campus = nameOf(options?.campuses, filters.campusId);
  if (campus) chips.push({ key: "campusId", label: "Campus", value: campus });
  const dept = nameOf(options?.departments, filters.departmentId);
  if (dept) chips.push({ key: "departmentId", label: "Department", value: dept });
  const course = nameOf(options?.courses, filters.courseId);
  if (course) chips.push({ key: "courseId", label: "Course", value: course });
  const building = nameOf(options?.buildings, filters.buildingId);
  if (building) chips.push({ key: "buildingId", label: "Building", value: building });
  const room = nameOf(options?.rooms, filters.roomId);
  if (room) chips.push({ key: "roomId", label: "Room", value: room });
  return chips;
};

/**
 * Compose live operational Executive Summary KPIs from existing command-center + executive cards.
 * Priority order per Prompt 4.
 */
export const composeOperationalExecutiveKpis = (data: EnterpriseDashboardExcellenceDto): DashboardWidgetDto[] => {
  const exec = data.executiveSummary?.cards ?? [];
  const today = data.commandCenter?.todaysOperations?.cards ?? [];
  const attendance = data.commandCenter?.attendanceOperations?.cards ?? [];
  const attention = data.commandCenter?.attentionRequired?.cards ?? [];

  const critical =
    findCard(exec, "exec-alerts") ??
    findCard(attention, "critical-system-alerts", "failed-attendance-sessions");
  const running = findCard(today, "classes-running-now") ?? findCard(attendance, "sessions-running");
  const completion =
    findCard(today, "attendance-completion-today") ?? findCard(exec, "exec-attendance");
  const pending =
    findCard(attendance, "attendance-review-queue") ??
    findCard(attention, "ai-recognition-queue");
  const faculty = findCard(today, "faculty-teaching-now");
  const scheduled = findCard(exec, "exec-classes");
  const health = findCard(exec, "exec-health");
  const recorded = findCard(attendance, "completed-today");

  const mapped: DashboardWidgetDto[] = [
    cloneAs(critical, "exec-critical-alerts", "Critical Alerts", "Alerts needing immediate attention"),
    cloneAs(running, "exec-classes-running", "Classes Running", "Classes currently in progress"),
    cloneAs(completion, "exec-attendance-completion", "Attendance Completion", "Completed vs started sessions today"),
    cloneAs(pending, "exec-pending-reviews", "Pending Reviews", "AI / attendance reviews awaiting action"),
    cloneAs(faculty, "exec-faculty-teaching", "Faculty Teaching", "Faculty associated with active sessions"),
    cloneAs(scheduled, "exec-scheduled-classes", "Scheduled Classes", "Total scheduled classes today"),
    cloneAs(health, "exec-platform-health", "Platform Health", "College system health status"),
    cloneAs(recorded, "exec-attendance-recorded", "Attendance Recorded", "Attendance sessions completed today"),
  ];

  return mapped.map((c, i) => ({ ...c, sortOrder: i }));
};

const numWord = (n: number) => {
  const words = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];
  if (n >= 0 && n < words.length) return words[n];
  return String(n);
};

const capitalize = (s: string) => (s ? s.charAt(0).toUpperCase() + s.slice(1) : s);

/** Human-readable Morning Brief from composed KPIs (no AI). */
export const composeMorningBrief = (
  kpis: DashboardWidgetDto[],
  summary?: ExecutiveSummaryDto | null,
): MorningBriefModel => {
  const by = (code: string) => kpis.find((k) => k.code === code);
  const scheduled = by("exec-scheduled-classes")?.value ?? summary?.totalScheduledClassesToday ?? 0;
  const running = by("exec-classes-running")?.value ?? 0;
  const completion =
    by("exec-attendance-completion")?.displayValue ?? summary?.overallAttendanceToday ?? "—";
  const alerts = by("exec-critical-alerts")?.value ?? summary?.criticalAlerts ?? 0;
  const pending = by("exec-pending-reviews")?.value ?? 0;

  const hour = new Date().getHours();
  const greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";

  const scheduledText =
    scheduled === 1 ? "There is 1 scheduled class today" : `There are ${scheduled} scheduled classes today`;
  const runningText =
    running === 0
      ? "No classes are currently running"
      : running === 1
        ? "One class is currently running"
        : `${capitalize(numWord(running))} classes are currently running`;
  const alertText =
    alerts === 0
      ? "No critical alerts require attention"
      : alerts === 1
        ? "One critical alert requires attention"
        : `${capitalize(numWord(alerts))} critical alerts require attention`;
  const pendingText =
    pending === 0
      ? "No AI attendance review sessions are pending"
      : pending === 1
        ? "One AI attendance review session is pending"
        : `${capitalize(numWord(pending))} AI attendance review sessions are pending`;

  const text = `${greeting}. ${scheduledText}. ${runningText}. Attendance completion is ${completion}. ${alertText}. ${pendingText}.`;

  return { text, generatedAt: new Date() };
};

export const sectionQuestion: Record<string, string> = {
  context: "Who am I operating for, and under what filters?",
  brief: "What should I know first this morning?",
  executive: "What is the current operational status?",
  attention: "What requires action?",
  timeline: "Where are we in today's academic day?",
  today: "What is happening now?",
  attendance: "How is attendance progressing?",
  scheduling: "What is the scheduling status?",
  visualizations: "What do historical trends show?",
  academic: "What academic resources are available?",
  health: "Is the platform healthy?",
  actions: "What can I do next?",
};
