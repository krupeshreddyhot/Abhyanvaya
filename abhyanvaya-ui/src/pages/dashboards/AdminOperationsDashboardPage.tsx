import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Accordion,
  AccordionDetails,
  AccordionSummary,
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  FormControl,
  IconButton,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  Typography,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import PauseIcon from "@mui/icons-material/Pause";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import axios from "axios";
import * as signalR from "@microsoft/signalr";
import { useNavigate } from "react-router-dom";
import { DashboardWidgetGrid } from "../../components/dashboards/DashboardWidgets";
import {
  AcademicTimelinePanel,
  ActionGroupsPanel,
  ActiveFilterContextPanel,
  AttentionRequiredPanel,
  DashboardFiltersBar,
  DashboardVisualizationsPanel,
  ExecutiveContextHeader,
  MorningBriefPanel,
  OperationalExecutiveSummary,
  WidgetHelpDrawer,
} from "../../components/dashboards/DashboardExcellencePanels";
import {
  composeMorningBrief,
  composeOperationalExecutiveKpis,
  resolveActiveFilters,
} from "../../components/dashboards/executiveInformationArchitecture";
import { fluidDashboardSx, sectionAccent, standardKpiColumns } from "../../components/dashboards/dashboardLayoutTokens";
import { useAuth } from "../../context/AuthContext";
import {
  exportAdminDashboardExcellence,
  getAdminDashboardExcellence,
  upsertDashboardPreferences,
  type CommandCenterSectionDto,
  type DashboardFilterRequest,
  type DashboardWidgetDto,
  type EnterpriseDashboardExcellenceDto,
  type WidgetHelpDto,
} from "../../services/enterpriseDashboardService";

const COLLAPSE_KEY = "ai31.8.2.commandCenter.collapsed";
const DISMISS_KEY = "ai31.8.2.commandCenter.dismissedBanners";

const loadJson = <T,>(key: string, fallback: T): T => {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
};

const bannerSeverity = (s?: string | null): "error" | "warning" | "info" | "success" => {
  if (s === "Red") return "error";
  if (s === "Orange" || s === "Yellow") return "warning";
  if (s === "Green") return "success";
  return "info";
};

const toQuery = (f: DashboardFilterRequest) => {
  const params = new URLSearchParams();
  if (f.academicYearId) params.set("academicYearId", String(f.academicYearId));
  if (f.departmentId) params.set("departmentId", String(f.departmentId));
  if (f.courseId) params.set("courseId", String(f.courseId));
  if (f.campusId) params.set("campusId", String(f.campusId));
  if (f.buildingId) params.set("buildingId", String(f.buildingId));
  if (f.roomId) params.set("roomId", String(f.roomId));
  return params.toString();
};

const GroupedCards = ({
  section,
  onHelp,
  filterQuery,
}: {
  section: CommandCenterSectionDto;
  onHelp: (w: DashboardWidgetDto) => void;
  filterQuery: string;
}) => {
  const groups = section.groupOrder?.length
    ? section.groupOrder
    : (Array.from(new Set((section.cards ?? []).map((c) => c.group).filter(Boolean))) as string[]);

  if (!groups.length) {
    return (
      <DashboardWidgetGrid
        widgets={section.cards}
        compact
        onHelp={onHelp}
        filterQuery={filterQuery}
        columns={standardKpiColumns}
        emptyMessage="No critical operational issues detected. All monitored workflows are operating normally."
      />
    );
  }

  return (
    <Stack spacing={1}>
      {groups.map((group) => {
        const cards = (section.cards ?? []).filter((c) => c.group === group);
        if (!cards.length) return null;
        return (
          <Box key={group}>
            <Typography variant="caption" sx={{ mb: 0.5, fontWeight: 700, textTransform: "uppercase", letterSpacing: 0.4 }}>
              {group}
            </Typography>
            <DashboardWidgetGrid
              widgets={cards}
              compact
              onHelp={onHelp}
              filterQuery={filterQuery}
              columns={standardKpiColumns}
            />
          </Box>
        );
      })}
    </Stack>
  );
};

const SectionAccordion = ({
  section,
  expanded,
  onToggle,
  onHelp,
  filterQuery,
}: {
  section: CommandCenterSectionDto;
  expanded: boolean;
  onToggle: (code: string, next: boolean) => void;
  onHelp: (w: DashboardWidgetDto) => void;
  filterQuery: string;
}) => {
  const navigate = useNavigate();
  const accent = sectionAccent[section.code] ?? sectionAccent.today;

  return (
    <Accordion
      expanded={expanded}
      onChange={(_, next) => onToggle(section.code, next)}
      disableGutters
      sx={{
        mb: 1,
        border: "1px solid",
        borderColor: "divider",
        borderLeft: `4px solid ${accent.border}`,
        borderRadius: 1,
        bgcolor: accent.tint,
        "&:before": { display: "none" },
      }}
    >
      <AccordionSummary expandIcon={<ExpandMoreIcon />} aria-controls={`${section.code}-content`} id={`${section.code}-header`}>
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
          <Typography component="span" aria-hidden>
            {section.icon ?? ""}
          </Typography>
          <Box>
            <Typography variant="subtitle1" sx={{ fontWeight: 800, color: accent.border }}>
              {section.title}
            </Typography>
            {section.subtitle && (
              <Typography variant="caption" color="text.secondary">
                {section.subtitle}
              </Typography>
            )}
          </Box>
          <Chip size="small" label={`${section.cards?.length ?? 0}`} sx={{ ml: 1, height: 20 }} />
        </Stack>
      </AccordionSummary>
      <AccordionDetails sx={{ pt: 0.5 }}>
        {section.code === "attendance" ? (
          <GroupedCards section={section} onHelp={onHelp} filterQuery={filterQuery} />
        ) : (
          <DashboardWidgetGrid
            widgets={section.cards}
            compact
            onHelp={onHelp}
            filterQuery={filterQuery}
            columns={standardKpiColumns}
            emptyMessage="No critical operational issues detected. All monitored workflows are operating normally."
          />
        )}
        {section.quickLinks?.length > 0 && (
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", mt: 1 }}>
            {section.quickLinks.map((l) => (
              <Button key={`${l.path}-${l.label}`} size="small" variant="outlined" onClick={() => navigate(l.path)}>
                {l.label}
              </Button>
            ))}
          </Stack>
        )}
      </AccordionDetails>
    </Accordion>
  );
};

const formatClock = (d: Date | null) =>
  d
    ? d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false })
    : "—";

/** AI31.8.2 — Executive Dashboard Information Architecture (presentation/composition only). */
const AdminOperationsDashboardPage = () => {
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const [data, setData] = useState<EnterpriseDashboardExcellenceDto | null>(null);
  const [filters, setFilters] = useState<DashboardFilterRequest>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>(() => loadJson(COLLAPSE_KEY, {}));
  const [dismissed, setDismissed] = useState<string[]>(() => loadJson(DISMISS_KEY, []));
  const [paused, setPaused] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const [helpWidget, setHelpWidget] = useState<DashboardWidgetDto | null>(null);
  const [helpDoc, setHelpDoc] = useState<WidgetHelpDto | null>(null);
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null);
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [toolbarExpanded, setToolbarExpanded] = useState(false);
  const [filterPanelExpanded, setFilterPanelExpanded] = useState(true);
  const [nowTick, setNowTick] = useState(() => Date.now());

  const load = useCallback(
    async (silent = false, nextFilters?: DashboardFilterRequest) => {
      if (!silent) {
        setLoading(true);
        setError(null);
      }
      try {
        const f = nextFilters ?? filters;
        const res = await getAdminDashboardExcellence(f);
        setData(res.data);
        setLastRefresh(new Date());
        if (res.data.preferences?.filters && !nextFilters && Object.values(filters).every((v) => v == null || v === undefined)) {
          setFilters(res.data.preferences.filters);
        }
      } catch (err) {
        if (axios.isAxiosError(err)) {
          const status = err.response?.status;
          setError(`Unable to load Enterprise Operations Dashboard${status ? ` (HTTP ${status})` : "."}`);
        } else {
          setError("Unable to load Enterprise Operations Dashboard.");
        }
      } finally {
        if (!silent) setLoading(false);
      }
    },
    [filters],
  );

  useEffect(() => {
    void load(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (paused) return;
    const seconds = data?.refreshIntervalSeconds ?? data?.preferences?.refreshIntervalSeconds ?? 60;
    if (!seconds || seconds <= 0) return;
    const id = window.setInterval(() => void load(true), Math.max(30, seconds) * 1000);
    return () => window.clearInterval(id);
  }, [data?.refreshIntervalSeconds, data?.preferences?.refreshIntervalSeconds, paused, load]);

  useEffect(() => {
    const id = window.setInterval(() => setNowTick(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, []);

  // Auto-collapse filter context panel after a short idle when filters are active.
  useEffect(() => {
    if (!Object.values(filters).some((v) => v != null)) return;
    const id = window.setTimeout(() => setFilterPanelExpanded(false), 8000);
    return () => window.clearTimeout(id);
  }, [filters]);

  useEffect(() => {
    const token = localStorage.getItem("token");
    if (!token) return;
    const baseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/api\/?$/, "") ?? "";
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/faculty`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();
    const refresh = () => {
      if (!paused) void load(true);
    };
    connection.on("FacultyScheduleNotification", refresh);
    connection.on("AttendanceRecoveryNotification", refresh);
    void connection.start().catch(() => undefined);
    return () => {
      void connection.stop();
    };
  }, [load, paused]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (!data || e.altKey || e.ctrlKey || e.metaKey) return;
      const tag = (e.target as HTMLElement | null)?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return;
      const key = e.key.toUpperCase();
      const actions = data.actionGroups.flatMap((g) => g.actions);
      const action = actions.find((a) => (a.shortcut ?? "").toUpperCase() === key);
      if (!action) return;
      if (action.requiredPermission && !hasPermission(action.requiredPermission)) return;
      e.preventDefault();
      navigate(action.path);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [data, hasPermission, navigate]);

  const cc = data?.commandCenter;

  const operationalKpis = useMemo(() => (data ? composeOperationalExecutiveKpis(data) : []), [data]);
  const morningBrief = useMemo(
    () => composeMorningBrief(operationalKpis, data?.executiveSummary),
    [operationalKpis, data?.executiveSummary],
  );
  const activeFilterChips = useMemo(() => resolveActiveFilters(filters, data?.filters), [filters, data?.filters]);

  const orderedSections = useMemo(() => {
    if (!cc) return [] as { key: string; section: CommandCenterSectionDto }[];
    return [
      { key: "today", section: cc.todaysOperations },
      { key: "attendance", section: cc.attendanceOperations },
      { key: "scheduling", section: cc.schedulingOperations },
      { key: "academic", section: cc.academicResources },
      { key: "health", section: cc.systemHealth },
    ];
  }, [cc]);

  const filterQuery = toQuery(filters);
  const highContrast = Boolean(data?.preferences?.highContrast);
  const refreshSeconds = data?.refreshIntervalSeconds ?? data?.preferences?.refreshIntervalSeconds ?? 60;
  const nextRefreshAt = useMemo(() => {
    void nowTick;
    if (paused || !lastRefresh || !refreshSeconds || refreshSeconds <= 0) return null;
    return new Date(lastRefresh.getTime() + Math.max(30, refreshSeconds) * 1000);
  }, [lastRefresh, refreshSeconds, paused, nowTick]);
  const currentTime = useMemo(() => formatClock(new Date(nowTick)), [nowTick]);

  const toggle = (code: string, next: boolean) => {
    setCollapsed((prev) => {
      const updated = { ...prev, [code]: !next };
      localStorage.setItem(COLLAPSE_KEY, JSON.stringify(updated));
      return updated;
    });
  };

  const dismissBanner = (code: string) => {
    setDismissed((prev) => {
      const updated = [...new Set([...prev, code])];
      localStorage.setItem(DISMISS_KEY, JSON.stringify(updated));
      return updated;
    });
  };

  const applyFilters = async () => {
    setFilterPanelExpanded(true);
    await upsertDashboardPreferences({ roleScope: "Admin", filters });
    await load(false, filters);
  };

  const clearFilters = async () => {
    const empty = {};
    setFilters(empty);
    await upsertDashboardPreferences({ roleScope: "Admin", filters: empty });
    await load(false, empty);
  };

  const setRefresh = async (seconds: number) => {
    await upsertDashboardPreferences({ roleScope: "Admin", refreshIntervalSeconds: seconds });
    await load(true);
  };

  const restoreDefaults = async () => {
    await upsertDashboardPreferences({ roleScope: "Admin", restoreDefaults: true });
    setFilters({});
    await load(false, {});
  };

  const exportDashboard = async (format: string) => {
    const res = await exportAdminDashboardExcellence({ format, filters });
    const blob = new Blob([res.data]);
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `enterprise-dashboard.${format === "excel" ? "xlsx" : format === "pdf" ? "txt" : "csv"}`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const openHelp = (w: DashboardWidgetDto) => {
    setHelpWidget(w);
    setHelpDoc(data?.widgetHelp.find((h) => h.widgetCode === w.code) ?? null);
    setHelpOpen(true);
  };

  const banners = (cc?.actionBanners ?? []).filter(
    (b) => !dismissed.includes(b.code) && (!b.requiredPermission || hasPermission(b.requiredPermission)),
  );

  if (loading && !data) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress aria-label="Loading dashboard" />
      </Box>
    );
  }

  // Section order (Prompt 6): after Attention + Timeline, Today → Attendance → Timetable,
  // then Analytics, then Academic + Health, then Quick Actions.
  const preAnalytics = orderedSections.filter((s) => s.key === "today" || s.key === "attendance" || s.key === "scheduling");
  const postAnalytics = orderedSections.filter((s) => s.key === "academic" || s.key === "health");

  return (
    <Box
      sx={{
        ...fluidDashboardSx,
        ...(highContrast
          ? {
              "& .MuiPaper-root": { borderWidth: 2 },
              "& .MuiTypography-root": { color: "text.primary" },
            }
          : {}),
      }}
    >
      <Stack spacing={0.15} sx={{ mb: 0.75 }}>
        <Typography variant="h5" sx={{ fontWeight: 800 }}>
          {data?.title ?? "Enterprise Operations Command Center"}
        </Typography>
        <Typography variant="caption" color="text.secondary">
          Executive information architecture — context, live KPIs, and actionable intelligence. Attendance modes unchanged.
        </Typography>
      </Stack>

      {/* Sticky Operations Toolbar */}
      <Paper
        sx={{
          p: { xs: 0.85, md: 1 },
          mb: 1,
          border: "1px solid",
          borderColor: "divider",
          position: "sticky",
          top: 0,
          zIndex: (t) => t.zIndex.appBar,
          bgcolor: "background.paper",
        }}
        component="section"
        aria-label="Operations toolbar"
      >
        <Stack
          direction="row"
          spacing={1}
          useFlexGap
          sx={{ flexWrap: "wrap", alignItems: "center", justifyContent: "space-between" }}
        >
          <Stack direction="row" spacing={1.5} sx={{ alignItems: "center" }}>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", lineHeight: 1.15 }}>
                Last Updated
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                {formatClock(lastRefresh)}
                {paused ? " · Paused" : ""}
              </Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", lineHeight: 1.15 }}>
                Next Refresh
              </Typography>
              <Typography variant="body2" sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>
                {formatClock(nextRefreshAt)}
              </Typography>
            </Box>
          </Stack>
          <Button
            size="small"
            variant="text"
            sx={{ display: { xs: "inline-flex", md: "none" } }}
            onClick={() => setToolbarExpanded((v) => !v)}
            aria-expanded={toolbarExpanded}
          >
            {toolbarExpanded ? "Hide tools" : "Tools"}
          </Button>
        </Stack>
        <Box sx={{ mt: 0.75, display: { xs: toolbarExpanded ? "block" : "none", md: "block" } }}>
          <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <FormControl size="small" sx={{ minWidth: 110 }}>
              <InputLabel>Refresh</InputLabel>
              <Select
                label="Refresh"
                value={data?.refreshIntervalSeconds ?? 60}
                onChange={(e) => void setRefresh(Number(e.target.value))}
              >
                <MenuItem value={30}>30 sec</MenuItem>
                <MenuItem value={60}>1 min</MenuItem>
                <MenuItem value={120}>2 min</MenuItem>
                <MenuItem value={300}>5 min</MenuItem>
                <MenuItem value={0}>Manual</MenuItem>
              </Select>
            </FormControl>
            <IconButton aria-label={paused ? "Resume refresh" : "Pause refresh"} onClick={() => setPaused((p) => !p)} size="small">
              {paused ? <PlayArrowIcon /> : <PauseIcon />}
            </IconButton>
            <Button size="small" variant="contained" onClick={() => void load(false)}>
              Refresh
            </Button>
            <Button size="small" variant={filtersOpen ? "contained" : "outlined"} onClick={() => setFiltersOpen((v) => !v)}>
              Filters
            </Button>
            <Button size="small" variant="outlined" onClick={() => void exportDashboard("excel")}>
              Export
            </Button>
            <Button size="small" variant="outlined" onClick={() => void exportDashboard("csv")}>
              Snapshot
            </Button>
            <Button size="small" variant="outlined" onClick={() => window.print()}>
              Print
            </Button>
            <Button size="small" variant="outlined" onClick={() => void restoreDefaults()}>
              Defaults
            </Button>
            <Button size="small" variant="outlined" onClick={() => navigate("/dashboard/preferences")}>
              Preferences
            </Button>
          </Stack>
        </Box>
        {filtersOpen && data?.filters && (
          <>
            <Divider sx={{ my: 1 }} />
            <DashboardFiltersBar
              embedded
              filters={data.filters}
              value={filters}
              onChange={setFilters}
              onApply={() => void applyFilters()}
              onClear={() => void clearFilters()}
            />
          </>
        )}
      </Paper>

      {error && (
        <Alert severity="error" sx={{ mb: 1 }}>
          {error}
        </Alert>
      )}

      {/* 1. Executive Context */}
      {data?.executiveSummary && (
        <ExecutiveContextHeader
          summary={data.executiveSummary}
          currentTime={currentTime}
          activeFilters={activeFilterChips}
        />
      )}

      <ActiveFilterContextPanel
        chips={activeFilterChips}
        expanded={filterPanelExpanded}
        onToggle={() => setFilterPanelExpanded((v) => !v)}
      />

      {/* 2. Morning Brief */}
      <MorningBriefPanel brief={morningBrief} />

      {/* 3. Executive Summary (operational KPIs only) */}
      <OperationalExecutiveSummary cards={operationalKpis} onHelp={openHelp} />

      {banners.length > 0 && (
        <Stack spacing={0.75} sx={{ mb: 1 }}>
          {banners.map((b) => (
            <Alert
              key={b.code}
              severity={bannerSeverity(b.severity)}
              action={
                <Stack direction="row" spacing={0.5} sx={{ alignItems: "center" }}>
                  <Button color="inherit" size="small" onClick={() => navigate(b.path)}>
                    {b.actionLabel}
                  </Button>
                  <IconButton size="small" color="inherit" aria-label="Dismiss" onClick={() => dismissBanner(b.code)}>
                    <CloseIcon fontSize="small" />
                  </IconButton>
                </Stack>
              }
            >
              {b.message}
            </Alert>
          ))}
        </Stack>
      )}

      {/* 4. Attention Required */}
      {cc?.attentionRequired && (
        <AttentionRequiredPanel cards={cc.attentionRequired.cards ?? []} filterQuery={filterQuery} />
      )}

      {/* 5. Today's Academic Timeline */}
      {data?.academicTimeline && <AcademicTimelinePanel timeline={data.academicTimeline} />}

      {/* 6–8. Today's Ops → Attendance → Timetable */}
      {preAnalytics.map(({ key, section }) => (
        <SectionAccordion
          key={key}
          section={section}
          expanded={collapsed[section.code] !== true}
          onToggle={toggle}
          onHelp={openHelp}
          filterQuery={filterQuery}
        />
      ))}

      {/* 9. Analytics (historical charts below operations) */}
      {data?.visualizations && <DashboardVisualizationsPanel viz={data.visualizations} />}

      {/* 10–11. Academic Resources → System Health */}
      {postAnalytics.map(({ key, section }) => (
        <SectionAccordion
          key={key}
          section={section}
          expanded={collapsed[section.code] !== true}
          onToggle={toggle}
          onHelp={openHelp}
          filterQuery={filterQuery}
        />
      ))}

      {/* 12. Quick Actions */}
      {data?.actionGroups && <ActionGroupsPanel groups={data.actionGroups} />}

      <WidgetHelpDrawer open={helpOpen} onClose={() => setHelpOpen(false)} help={helpDoc} widget={helpWidget} />
    </Box>
  );
};

export default AdminOperationsDashboardPage;
