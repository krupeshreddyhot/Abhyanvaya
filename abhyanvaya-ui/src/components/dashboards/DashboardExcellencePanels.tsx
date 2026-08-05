import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  Drawer,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import EventNoteIcon from "@mui/icons-material/EventNote";
import ScheduleIcon from "@mui/icons-material/Schedule";
import AdminPanelSettingsIcon from "@mui/icons-material/AdminPanelSettings";
import SettingsSuggestIcon from "@mui/icons-material/SettingsSuggest";
import ErrorIcon from "@mui/icons-material/Error";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import { useNavigate } from "react-router-dom";
import type {
  AcademicTimelineDto,
  ActionGroupDto,
  CommandCenterQuickActionDto,
  DashboardFilterRequest,
  DashboardFilterStateDto,
  DashboardVisualizationsDto,
  DashboardWidgetDto,
  ExecutiveSummaryDto,
  OperationalChartSeriesDto,
  WidgetHelpDto,
} from "../../services/enterpriseDashboardService";
import { DashboardWidgetGrid } from "./DashboardWidgets";
import { useAuth } from "../../context/AuthContext";
import { denseKpiColumns, sectionAccent, severityRank } from "./dashboardLayoutTokens";
import type { ActiveFilterChip, MorningBriefModel } from "./executiveInformationArchitecture";

const fmtTime = (t?: string | null) => {
  if (!t) return "—";
  return t.length >= 5 ? t.slice(0, 5) : t;
};

/** AI31.8.2 Prompt 1 — compact context ribbon (≤70px, no KPI styling). */
export const ExecutiveContextHeader = ({
  summary,
  currentTime,
  activeFilters,
}: {
  summary: ExecutiveSummaryDto;
  currentTime: string;
  activeFilters: ActiveFilterChip[];
}) => {
  const accent = sectionAccent.context;
  const dateLabel = summary.todaysDate
    ? new Date(summary.todaysDate).toLocaleDateString(undefined, {
        weekday: "short",
        day: "2-digit",
        month: "short",
        year: "numeric",
      })
    : "—";

  const items: { label: string; value: string }[] = [
    { label: "College", value: summary.collegeName ?? "—" },
    { label: "Academic Year", value: summary.academicYear ?? "—" },
    { label: "Date", value: dateLabel },
    { label: "Time", value: currentTime },
  ];
  for (const f of activeFilters) {
    if (f.key === "campusId" || f.key === "departmentId") {
      items.push({ label: f.label, value: f.value });
    }
  }

  return (
    <Box
      sx={{
        mb: 1,
        px: 1.1,
        py: 0.65,
        maxHeight: "var(--dash-context-max-h, 70px)",
        overflow: "hidden",
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `3px solid ${accent.border}`,
        bgcolor: accent.tint,
        borderRadius: 1,
        display: "flex",
        alignItems: "center",
      }}
      component="section"
      aria-label="Executive Context"
    >
      <Stack
        direction="row"
        spacing={1.25}
        useFlexGap
        sx={{ flexWrap: "wrap", alignItems: "center", width: "100%", rowGap: 0.5 }}
      >
        {items.map((item) => (
          <Stack key={item.label} direction="row" spacing={0.5} sx={{ alignItems: "baseline" }}>
            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, lineHeight: 1.2 }}>
              {item.label}
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 700, lineHeight: 1.2, fontSize: "0.85rem" }}>
              {item.value}
            </Typography>
          </Stack>
        ))}
        {activeFilters.length > 0 && (
          <Chip size="small" label={`${activeFilters.length} filter${activeFilters.length === 1 ? "" : "s"}`} sx={{ height: 20 }} />
        )}
      </Stack>
    </Box>
  );
};

/** AI31.8.2 Prompt 9 — active filters only; collapses when empty / idle. */
export const ActiveFilterContextPanel = ({
  chips,
  expanded,
  onToggle,
}: {
  chips: ActiveFilterChip[];
  expanded: boolean;
  onToggle: () => void;
}) => {
  if (!chips.length) return null;
  const accent = sectionAccent.context;
  return (
    <Paper
      variant="outlined"
      sx={{
        mb: 1,
        px: 1,
        py: 0.6,
        borderLeft: `3px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
      component="section"
      aria-label="Active filters"
    >
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", justifyContent: "space-between" }}>
        <Typography variant="caption" sx={{ fontWeight: 700 }}>
          Active Filters
        </Typography>
        <Button size="small" variant="text" onClick={onToggle} sx={{ minHeight: 24, py: 0 }}>
          {expanded ? "Collapse" : "Expand"}
        </Button>
      </Stack>
      {expanded && (
        <Stack direction="row" spacing={0.75} useFlexGap sx={{ flexWrap: "wrap", mt: 0.5 }}>
          {chips.map((c) => (
            <Chip key={c.key} size="small" variant="outlined" label={`${c.label}: ${c.value}`} sx={{ height: 22 }} />
          ))}
        </Stack>
      )}
    </Paper>
  );
};

/** AI31.8.2 Prompt 3 — rule-based morning brief from existing metrics (no AI). */
export const MorningBriefPanel = ({ brief }: { brief: MorningBriefModel }) => {
  const accent = sectionAccent.brief;
  return (
    <Paper
      sx={{
        p: 1.1,
        mb: 1,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
      component="section"
      aria-label="Morning Brief"
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 800, color: accent.border, mb: 0.35 }}>
        Morning Brief
      </Typography>
      <Typography variant="body2" sx={{ lineHeight: 1.45 }}>
        {brief.text}
      </Typography>
    </Paper>
  );
};

/** AI31.8.2 Prompt 2 — operational Executive Summary only. */
export const OperationalExecutiveSummary = ({
  cards,
  onHelp,
}: {
  cards: DashboardWidgetDto[];
  onHelp?: (w: DashboardWidgetDto) => void;
}) => {
  const accent = sectionAccent.executive;
  return (
    <Paper
      sx={{
        p: { xs: 0.85, md: 1 },
        mb: 1,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
      component="section"
      aria-label="Executive Summary"
    >
      <Typography variant="subtitle2" sx={{ fontWeight: 800, color: accent.border, mb: 0.65 }}>
        Executive Summary
      </Typography>
      <DashboardWidgetGrid widgets={cards} executive size="sm" onHelp={onHelp} columns={denseKpiColumns} />
    </Paper>
  );
};

/** @deprecated AI31.8.1A — retained export alias for compatibility. */
export const ExecutiveSummaryHeader = OperationalExecutiveSummary;
/** @deprecated AI31.8.1A institutional strip removed; context header replaces it. */
export const RelocatedExecutiveKpis = (_props: {
  summary?: ExecutiveSummaryDto;
  onHelp?: (w: DashboardWidgetDto) => void;
}) => null;

export const ViewportTrendPreview = ({ viz }: { viz: DashboardVisualizationsDto }) => {
  const series =
    viz.weeklyAttendanceTrend?.points?.length
      ? viz.weeklyAttendanceTrend
      : viz.schedulingCompletion?.points?.length
        ? viz.schedulingCompletion
        : viz.attendanceHeatmap;
  const accent = sectionAccent.today;

  return (
    <Paper
      sx={{
        p: 1.1,
        mb: 1.25,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
        height: 148,
      }}
      component="section"
      aria-label="Priority trend"
    >
      <Typography variant="caption" sx={{ fontWeight: 700, color: accent.border }}>
        {series?.title ?? "7-day attendance trend"}
      </Typography>
      {!series?.points?.length ? (
        <Alert severity="info" variant="outlined" sx={{ mt: 0.75, py: 0.25 }}>
          Attendance analytics will appear after attendance sessions are completed.
        </Alert>
      ) : (
        <ResponsiveContainer width="100%" height="82%">
          <BarChart data={series.points}>
            <CartesianGrid strokeDasharray="3 3" vertical={false} />
            <XAxis dataKey="label" hide={series.points.length > 10} tick={{ fontSize: 10 }} />
            <YAxis allowDecimals={false} width={24} tick={{ fontSize: 10 }} />
            <Tooltip />
            <Bar dataKey="value" fill={accent.border} radius={[3, 3, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      )}
    </Paper>
  );
};

export const DashboardFiltersBar = ({
  filters,
  value,
  onChange,
  onApply,
  onClear,
  embedded,
}: {
  filters: DashboardFilterStateDto;
  value: DashboardFilterRequest;
  onChange: (next: DashboardFilterRequest) => void;
  onApply: () => void;
  onClear: () => void;
  embedded?: boolean;
}) => {
  const field = (
    label: string,
    key: keyof DashboardFilterRequest,
    options: { id: number; name: string }[],
  ) => (
    <FormControl size="small" sx={{ minWidth: { xs: "46%", sm: 120 } }}>
      <InputLabel>{label}</InputLabel>
      <Select
        label={label}
        value={value[key] != null ? String(value[key]) : ""}
        onChange={(e) =>
          onChange({
            ...value,
            [key]: e.target.value === "" ? null : Number(e.target.value),
          })
        }
      >
        <MenuItem value="">All</MenuItem>
        {options.map((o) => (
          <MenuItem key={o.id} value={String(o.id)}>
            {o.name}
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  );

  const body = (
    <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
      {field("Academic Year", "academicYearId", filters.academicYears)}
      {field("Department", "departmentId", filters.departments)}
      {field("Course", "courseId", filters.courses)}
      {field("Campus", "campusId", filters.campuses)}
      {field("Building", "buildingId", filters.buildings)}
      {field("Room", "roomId", filters.rooms)}
      <Button variant="contained" size="small" onClick={onApply}>
        Apply
      </Button>
      <Button variant="outlined" size="small" onClick={onClear}>
        Clear
      </Button>
    </Stack>
  );

  if (embedded) return body;
  return (
    <Paper sx={{ p: 1.25, mb: 1.25 }} component="section" aria-label="Dashboard filters">
      {body}
    </Paper>
  );
};

/** Compact enterprise operational timeline (presentation remap of period timeline). */
export const AcademicTimelinePanel = ({ timeline }: { timeline: AcademicTimelineDto }) => {
  const accent = sectionAccent.timeline;
  const stages = [
    { label: "Faculty Login", kind: "Stage" },
    { label: "Classes Started", kind: "Stage" },
    { label: "Attendance", kind: "Stage" },
    { label: "Recognition", kind: "Stage" },
    { label: "Recovery", kind: "Stage" },
    { label: "Completed", kind: "Stage" },
  ];

  // Map current period into stage highlight without changing backend data.
  const currentIdx = timeline.items.findIndex((i) => i.isCurrent);
  const activeStage =
    currentIdx < 0 ? 0 : currentIdx <= 1 ? 1 : currentIdx <= 3 ? 2 : currentIdx <= 5 ? 3 : currentIdx <= 7 ? 4 : 5;

  return (
    <Paper
      sx={{
        p: 1.1,
        mb: 1.25,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
      component="section"
      aria-label="Operational timeline"
    >
      <Stack direction="row" spacing={1} sx={{ justifyContent: "space-between", mb: 0.75, alignItems: "center" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 800, color: accent.border }}>
          Today&apos;s Operations Timeline
        </Typography>
        <Chip
          size="small"
          label={`${fmtTime(timeline.currentTime)} · ${timeline.currentPeriodLabel ?? "—"}`}
          color="primary"
          sx={{ height: 22 }}
        />
      </Stack>
      <Box sx={{ overflowX: "auto" }}>
        <Stack direction="row" spacing={0} sx={{ minWidth: 640, alignItems: "center" }}>
          {stages.map((s, idx) => (
            <Stack key={s.label} direction="row" sx={{ alignItems: "center", flex: 1 }}>
              <Paper
                elevation={0}
                sx={{
                  px: 1,
                  py: 0.75,
                  flex: 1,
                  textAlign: "center",
                  border: "1px solid",
                  borderColor: idx === activeStage ? accent.border : "divider",
                  bgcolor: idx === activeStage ? "action.selected" : "background.paper",
                  borderRadius: 1,
                }}
                aria-current={idx === activeStage ? "step" : undefined}
              >
                <Typography variant="caption" sx={{ fontWeight: idx === activeStage ? 800 : 600, fontSize: "0.72rem" }}>
                  {s.label}
                </Typography>
              </Paper>
              {idx < stages.length - 1 && (
                <Typography aria-hidden sx={{ px: 0.5, color: "text.secondary", fontWeight: 700 }}>
                  →
                </Typography>
              )}
            </Stack>
          ))}
        </Stack>
      </Box>
      {/* Keep period detail as secondary compact strip */}
      <Box sx={{ overflowX: "auto", mt: 1 }}>
        <Stack direction="row" spacing={0.75} sx={{ minWidth: 560 }}>
          {timeline.items
            .filter((i) => i.kind === "Period")
            .map((item, idx) => (
              <Chip
                key={`${item.label}-${idx}`}
                size="small"
                variant={item.isCurrent ? "filled" : "outlined"}
                color={item.isCurrent ? "primary" : "default"}
                label={`${item.label} ${fmtTime(item.startTime)}`}
              />
            ))}
        </Stack>
      </Box>
    </Paper>
  );
};

const AttentionSeverityIcon = ({ status }: { status?: string | null }) => {
  if (status === "Red") return <ErrorIcon color="error" fontSize="small" />;
  if (status === "Orange" || status === "Yellow") return <WarningAmberIcon color="warning" fontSize="small" />;
  return <InfoOutlinedIcon color="info" fontSize="small" />;
};

/** Attention Required — severity-sorted actionable cards. */
export const AttentionRequiredPanel = ({
  cards,
  filterQuery,
}: {
  cards: DashboardWidgetDto[];
  filterQuery?: string;
}) => {
  const navigate = useNavigate();
  const accent = sectionAccent.attention;
  const sorted = [...cards]
    .filter((c) => (c.value ?? 0) > 0 && c.status !== "Green")
    .sort((a, b) => severityRank(a.status) - severityRank(b.status) || (b.value ?? 0) - (a.value ?? 0));

  const open = (c: DashboardWidgetDto) => {
    const path = c.path ?? "/dashboard";
    navigate(filterQuery ? `${path}${path.includes("?") ? "&" : "?"}${filterQuery}` : path);
  };

  return (
    <Paper
      sx={{
        p: 1.25,
        mb: 1.25,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
      component="section"
      aria-label="Attention Required"
    >
      <Typography variant="subtitle1" sx={{ fontWeight: 800, color: accent.border, mb: 1 }}>
        🚨 Attention Required
      </Typography>
      {sorted.length === 0 ? (
        <Alert severity="success" variant="outlined">
          No critical operational issues detected. All monitored workflows are operating normally.
        </Alert>
      ) : (
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", md: "1fr 1fr", xl: "1fr 1fr 1fr" },
            gap: "var(--dash-gap, 10px)",
          }}
        >
          {sorted.map((c) => (
            <Paper
              key={c.code}
              sx={{
                p: 1.25,
                minHeight: "var(--dash-card-md, 180px)",
                borderLeft: "3px solid",
                borderLeftColor: c.status === "Red" ? "error.main" : c.status === "Orange" ? "#ed6c02" : "warning.main",
              }}
            >
              <Stack spacing={0.75} sx={{ height: "100%" }}>
                <Stack direction="row" spacing={0.75} sx={{ alignItems: "center" }}>
                  <AttentionSeverityIcon status={c.status} />
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {c.title}
                  </Typography>
                  <Chip size="small" label={c.statusLabel ?? c.status} sx={{ ml: "auto", height: 20 }} />
                </Stack>
                <Typography sx={{ fontWeight: 800, fontSize: "1.75rem", lineHeight: 1.1 }}>
                  {c.displayValue ?? c.value ?? "0"}
                  {c.unit ? (
                    <Typography component="span" variant="body2" color="text.secondary" sx={{ ml: 0.75, fontWeight: 600 }}>
                      {c.unit}
                    </Typography>
                  ) : null}
                </Typography>
                {c.estimatedImpact && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    Impact: {c.estimatedImpact}
                  </Typography>
                )}
                {(c.suggestedAction || c.explanation) && (
                  <Typography variant="caption" sx={{ display: "block", fontWeight: 600 }}>
                    {c.suggestedAction ?? c.explanation}
                  </Typography>
                )}
                <Box sx={{ mt: "auto" }}>
                  <Button size="small" variant="contained" color={c.status === "Red" ? "error" : "warning"} onClick={() => open(c)}>
                    Review Now
                  </Button>
                </Box>
              </Stack>
            </Paper>
          ))}
        </Box>
      )}
    </Paper>
  );
};

const ChartCard = ({ series, emptyHint }: { series?: OperationalChartSeriesDto | null; emptyHint?: string }) => {
  if (!series || !series.points?.length) {
    return (
      <Paper sx={{ p: 1.25, height: 220 }}>
        <Typography variant="subtitle2">{series?.title ?? "Chart"}</Typography>
        <Alert severity="info" variant="outlined" sx={{ mt: 1, py: 0.5 }}>
          {emptyHint ?? "Attendance analytics will appear after attendance sessions are completed."}
        </Alert>
      </Paper>
    );
  }
  return (
    <Paper sx={{ p: 1.25, height: 240 }}>
      <Typography variant="subtitle2" sx={{ mb: 0.5, fontWeight: 700 }}>
        {series.title}
      </Typography>
      <ResponsiveContainer width="100%" height="85%">
        <BarChart data={series.points}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="label" hide={series.points.length > 8} />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Bar dataKey="value" fill="#1976d2" radius={[4, 4, 0, 0]} />
        </BarChart>
      </ResponsiveContainer>
    </Paper>
  );
};

export const DashboardVisualizationsPanel = ({ viz }: { viz: DashboardVisualizationsDto }) => {
  const accent = sectionAccent.visualizations;
  return (
    <Accordion
      defaultExpanded={false}
      disableGutters
      sx={{
        mb: 1.25,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
    >
      <AccordionSummary expandIcon={<ExpandMoreIcon />}>
        <Typography variant="subtitle1" sx={{ fontWeight: 800 }}>
          Heatmaps &amp; Executive Visualizations
        </Typography>
      </AccordionSummary>
      <AccordionDetails>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: { xs: "1fr", md: "1fr 1fr", xl: "1fr 1fr 1fr" },
            gap: 1.1,
          }}
        >
          <ChartCard series={viz.attendanceHeatmap} />
          <ChartCard series={viz.departmentHeatmap} emptyHint="Department analytics will appear as data accumulates." />
          <ChartCard series={viz.facultyWorkloadHeatmap} emptyHint="Faculty workload signals will appear once timetable load is available." />
          <ChartCard series={viz.roomUtilizationHeatmap} emptyHint="Room utilization will appear once rooms are scheduled." />
          <ChartCard series={viz.weeklyAttendanceTrend} />
          <ChartCard series={viz.schedulingCompletion} emptyHint="Scheduling completion trends will appear after timetable activity." />
          <ChartCard series={viz.conflictTrend} emptyHint="No scheduling issues detected for the current scope." />
        </Box>
      </AccordionDetails>
    </Accordion>
  );
};

export const refineActionGroups = (incoming: ActionGroupDto[]): ActionGroupDto[] => {
  const all = incoming.flatMap((g) => g.actions);
  const byCode = (codes: string[]) =>
    codes.map((c) => all.find((a) => a.code === c)).filter((a): a is CommandCenterQuickActionDto => Boolean(a));

  return [
    {
      code: "attendance",
      title: "Attendance",
      actions: [
        ...byCode(["take-attendance", "attendance-recovery", "review-attendance"]),
        { code: "attendance-reports", label: "Attendance Reports", path: "/reports", requiredPermission: "ReportsView", primary: false },
      ],
    },
    {
      code: "scheduling",
      title: "Scheduling",
      actions: [
        ...byCode(["create-timetable"]),
        { code: "schedule-versions", label: "Schedule Versions", path: "/setup/scheduling/governance/versions", requiredPermission: "SchedulingManage", primary: false },
        { code: "conflict-workspace", label: "Conflict Workspace", path: "/setup/scheduling/conflicts/workspace", requiredPermission: "SchedulingManage", primary: false },
        ...byCode(["approve-timetable", "run-optimization"]),
      ],
    },
    {
      code: "administration",
      title: "Administration",
      actions: [
        { code: "catalog", label: "Catalog", path: "/setup", requiredPermission: "DashboardView", primary: false },
        { code: "users", label: "Users", path: "/setup/users", requiredPermission: "DashboardView", primary: false },
        { code: "roles", label: "Roles", path: "/setup/roles", requiredPermission: "DashboardView", primary: false },
      ],
    },
    {
      code: "operations",
      title: "Operations",
      actions: [
        ...byCode(["notifications", "reports"]),
        { code: "health", label: "Health Center", path: "/dashboard/health", requiredPermission: "DashboardView", primary: false, shortcut: "H" },
      ],
    },
  ];
};

const groupIcon = (code: string) => {
  if (code === "attendance") return <EventNoteIcon fontSize="small" />;
  if (code === "scheduling") return <ScheduleIcon fontSize="small" />;
  if (code === "administration") return <AdminPanelSettingsIcon fontSize="small" />;
  return <SettingsSuggestIcon fontSize="small" />;
};

export const ActionGroupsPanel = ({ groups }: { groups: ActionGroupDto[] }) => {
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const refined = refineActionGroups(groups);
  const accent = sectionAccent.actions;

  return (
    <Paper
      sx={{
        p: 1.25,
        mb: 1.25,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        bgcolor: accent.tint,
      }}
      component="section"
      aria-label="Quick Actions"
    >
      <Typography variant="subtitle1" sx={{ mb: 1, fontWeight: 800, color: accent.border }}>
        Quick Actions
      </Typography>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr", lg: "1fr 1fr 1fr 1fr" },
          gap: 1.1,
        }}
      >
        {refined.map((g) => (
          <Paper
            key={g.code}
            variant="outlined"
            sx={{
              p: 1.1,
              minHeight: "var(--dash-card-md, 180px)",
              transition: "border-color 120ms ease, box-shadow 120ms ease",
              "&:hover": { borderColor: accent.border, boxShadow: 1 },
            }}
          >
            <Stack direction="row" spacing={0.75} sx={{ alignItems: "center", mb: 0.75 }}>
              {groupIcon(g.code)}
              <Typography variant="subtitle2" sx={{ fontWeight: 800 }}>
                {g.title}
              </Typography>
            </Stack>
            <Stack spacing={0.5}>
              {g.actions
                .filter((a) => !a.requiredPermission || hasPermission(a.requiredPermission))
                .map((a) => (
                  <Button
                    key={a.code}
                    size="small"
                    variant={a.primary ? "contained" : "text"}
                    onClick={() => navigate(a.path)}
                    title={a.shortcut ? `Shortcut: ${a.shortcut}` : a.label}
                    sx={{ justifyContent: "flex-start", minHeight: 34 }}
                  >
                    {a.label}
                    {a.shortcut ? (
                      <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 0.75 }}>
                        {a.shortcut}
                      </Typography>
                    ) : null}
                  </Button>
                ))}
            </Stack>
          </Paper>
        ))}
      </Box>
    </Paper>
  );
};

export const WidgetHelpDrawer = ({
  open,
  onClose,
  help,
  widget,
}: {
  open: boolean;
  onClose: () => void;
  help?: WidgetHelpDto | null;
  widget?: DashboardWidgetDto | null;
}) => {
  const navigate = useNavigate();
  return (
    <Drawer anchor="right" open={open} onClose={onClose}>
      <Box sx={{ p: 2, width: { xs: "100vw", sm: 380 }, maxWidth: "100%" }} role="dialog" aria-label="Widget help">
        <Typography variant="h6" gutterBottom>
          {widget?.title ?? help?.widgetCode ?? "Widget Help"}
        </Typography>
        <Typography variant="subtitle2">Purpose</Typography>
        <Typography variant="body2" sx={{ mb: 1.5 }}>
          {help?.purpose ?? widget?.explanation ?? widget?.tooltip ?? "Operational KPI for college administrators."}
        </Typography>
        <Typography variant="subtitle2">How calculated</Typography>
        <Typography variant="body2" sx={{ mb: 1.5 }}>
          {help?.howCalculated ?? "Composed from existing dashboard services — no new business logic."}
        </Typography>
        <Stack spacing={1}>
          {(help?.navigationLinks ?? []).map((l) => (
            <Button key={l.path} variant="outlined" onClick={() => navigate(l.path)}>
              {l.label}
            </Button>
          ))}
          {widget?.path && (
            <Button variant="contained" onClick={() => navigate(widget.path!)}>
              Open Module
            </Button>
          )}
        </Stack>
      </Box>
    </Drawer>
  );
};
