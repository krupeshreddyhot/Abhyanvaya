import {
  Alert,
  Box,
  Chip,
  IconButton,
  Menu,
  MenuItem,
  Paper,
  Stack,
  Tooltip,
  Typography,
} from "@mui/material";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import PushPinIcon from "@mui/icons-material/PushPin";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";
import ErrorIcon from "@mui/icons-material/Error";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import TrendingUpIcon from "@mui/icons-material/TrendingUp";
import SchoolIcon from "@mui/icons-material/School";
import EventAvailableIcon from "@mui/icons-material/EventAvailable";
import GroupsIcon from "@mui/icons-material/Groups";
import HealthAndSafetyIcon from "@mui/icons-material/HealthAndSafety";
import FactCheckIcon from "@mui/icons-material/FactCheck";
import { useState, type MouseEvent, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import type { DashboardWidgetDto } from "../../services/enterpriseDashboardService";
import { resolveDrilldownPath } from "../../utils/dashboardNavigation";
import { trendGlyph } from "./dashboardLayoutTokens";

const statusColor = (status?: string | null) => {
  if (status === "Red") return "error";
  if (status === "Orange" || status === "Yellow") return "warning";
  if (status === "Green") return "success";
  if (status === "Info") return "info";
  return "default";
};

const borderColor = (status?: string | null) => {
  if (status === "Red") return "error.main";
  if (status === "Orange") return "#ed6c02";
  if (status === "Yellow") return "warning.main";
  if (status === "Green") return "success.main";
  if (status === "Info") return "info.main";
  return "primary.main";
};

const StatusIcon = ({ status }: { status?: string | null }) => {
  if (status === "Red") return <ErrorIcon sx={{ fontSize: 16 }} color="error" />;
  if (status === "Orange" || status === "Yellow") return <WarningAmberIcon sx={{ fontSize: 16 }} color="warning" />;
  if (status === "Green") return <CheckCircleIcon sx={{ fontSize: 16 }} color="success" />;
  return <InfoOutlinedIcon sx={{ fontSize: 16 }} color="action" />;
};

const kpiIcon = (code: string): ReactNode => {
  if (code.includes("alert") || code.includes("critical")) return <WarningAmberIcon sx={{ fontSize: 18 }} color="error" />;
  if (code.includes("running") || code.includes("teaching")) return <GroupsIcon sx={{ fontSize: 18 }} color="primary" />;
  if (code.includes("completion") || code.includes("attendance")) return <FactCheckIcon sx={{ fontSize: 18 }} color="success" />;
  if (code.includes("review") || code.includes("pending")) return <EventAvailableIcon sx={{ fontSize: 18 }} color="warning" />;
  if (code.includes("health")) return <HealthAndSafetyIcon sx={{ fontSize: 18 }} color="action" />;
  if (code.includes("scheduled") || code.includes("class")) return <SchoolIcon sx={{ fontSize: 18 }} color="primary" />;
  return <TrendingUpIcon sx={{ fontSize: 18 }} color="action" />;
};

type Size = "sm" | "md" | "lg";

type GridProps = {
  widgets: DashboardWidgetDto[];
  compact?: boolean;
  rich?: boolean;
  executive?: boolean;
  size?: Size;
  columns?: { xs?: number; sm?: number; md?: number; lg?: number; xl?: number };
  onHelp?: (widget: DashboardWidgetDto) => void;
  filterQuery?: string;
  emptyMessage?: string;
  /** Show per-card timestamps (default false — toolbar owns Last Updated). */
  showTimestamps?: boolean;
};

const minHeight = (size: Size, executive?: boolean) => {
  if (executive) return "var(--dash-card-sm, 112px)";
  if (size === "lg") return "var(--dash-card-lg, 240px)";
  if (size === "md") return "var(--dash-card-md, 160px)";
  return "var(--dash-card-sm, 112px)";
};

/** AI31.8.2 — Executive KPI design system: Icon/Status · Value · Subtitle/Trend/Action. */
export const DashboardWidgetGrid = ({
  widgets,
  compact,
  rich: _rich = true,
  executive,
  size,
  columns,
  onHelp,
  filterQuery,
  emptyMessage,
  showTimestamps = false,
}: GridProps) => {
  const navigate = useNavigate();
  const visible = widgets.filter((w) => w.visible !== false);
  const [menu, setMenu] = useState<{ anchor: HTMLElement; widget: DashboardWidgetDto } | null>(null);
  const cardSize: Size = size ?? (executive ? "sm" : compact ? "sm" : "md");

  const withFilters = (path: string) => (filterQuery ? `${path}${path.includes("?") ? "&" : "?"}${filterQuery}` : path);

  const openPath = (w: DashboardWidgetDto, kind: "details" | "module" | "report" | "help" = "details") => {
    if (kind === "help") {
      onHelp?.(w);
      return;
    }
    if (kind === "report" && w.reportPath) {
      navigate(withFilters(w.reportPath));
      return;
    }
    navigate(withFilters(resolveDrilldownPath(w.code, w.path)));
  };

  const cols = columns ?? {
    xs: 1,
    sm: 2,
    md: executive ? 4 : compact ? 3 : 3,
    lg: 4,
    xl: 4,
  };

  if (visible.length === 0) {
    return (
      <Alert severity="success" variant="outlined" sx={{ mb: 1 }}>
        {emptyMessage ?? "No critical operational issues detected. All monitored workflows are operating normally."}
      </Alert>
    );
  }

  return (
    <>
      <Box
        sx={{
          display: "grid",
          gridTemplateColumns: {
            xs: `repeat(${cols.xs ?? 1}, minmax(0, 1fr))`,
            sm: `repeat(${cols.sm ?? 2}, minmax(0, 1fr))`,
            md: `repeat(${cols.md ?? 3}, minmax(0, 1fr))`,
            lg: `repeat(${cols.lg ?? 4}, minmax(0, 1fr))`,
            xl: `repeat(${cols.xl ?? 4}, minmax(0, 1fr))`,
          },
          gap: "var(--dash-gap, 8px)",
        }}
      >
        {visible.map((w) => {
          const label = w.statusLabel ?? w.status;
          const glyph = trendGlyph(w.trend);
          const showBadge = label && label !== "Information" && w.status !== "Info";
          const hoverDetail = [w.explanation, w.tooltip, w.estimatedImpact].filter(Boolean).join(" · ");

          const card = (
            <Paper
              sx={{
                p: executive ? 1 : 1.15,
                cursor: w.path ? "pointer" : "default",
                borderLeft: "3px solid",
                borderLeftColor: borderColor(w.status),
                height: "100%",
                minHeight: minHeight(cardSize, executive),
                display: "flex",
                flexDirection: "column",
                outline: "none",
                transition: "box-shadow 120ms ease, transform 120ms ease",
                "&:hover": w.path
                  ? {
                      boxShadow: 2,
                      "& .kpi-action": { opacity: 1 },
                    }
                  : undefined,
                "&:focus-visible": { boxShadow: 4 },
              }}
              onClick={() => w.path && openPath(w)}
              onContextMenu={(e: MouseEvent) => {
                if (!w.path && !w.reportPath && !onHelp) return;
                e.preventDefault();
                setMenu({ anchor: e.currentTarget as HTMLElement, widget: w });
              }}
              onKeyDown={(e) => {
                if (w.path && (e.key === "Enter" || e.key === " ")) {
                  e.preventDefault();
                  openPath(w);
                }
              }}
              role={w.path ? "link" : "group"}
              tabIndex={w.path ? 0 : undefined}
              aria-label={`${w.title}: ${w.displayValue ?? w.value ?? "—"} ${w.unit ?? ""}`}
            >
              <Stack spacing={0.35} sx={{ flex: 1 }}>
                {/* Top: Icon + Status */}
                <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
                  <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", minWidth: 0 }}>
                    {kpiIcon(w.code)}
                    {showBadge ? (
                      <Chip size="small" label={label} color={statusColor(w.status)} sx={{ height: 18, "& .MuiChip-label": { px: 0.6, fontSize: "0.65rem" } }} />
                    ) : (
                      <StatusIcon status={w.status} />
                    )}
                  </Stack>
                  {(w.path || onHelp) && (
                    <IconButton
                      size="small"
                      aria-label={`More actions for ${w.title}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        setMenu({ anchor: e.currentTarget, widget: w });
                      }}
                      sx={{ minWidth: 28, minHeight: 28 }}
                    >
                      <MoreVertIcon sx={{ fontSize: 16 }} />
                    </IconButton>
                  )}
                </Stack>

                {/* Middle: Large value */}
                <Stack direction="row" spacing={0.6} sx={{ alignItems: "baseline" }} useFlexGap>
                  <Typography
                    sx={{
                      fontWeight: 800,
                      fontSize: executive ? "1.45rem" : "1.65rem",
                      lineHeight: 1.05,
                      letterSpacing: "-0.03em",
                    }}
                  >
                    {w.displayValue ?? w.value ?? "—"}
                  </Typography>
                  {glyph && (
                    <Typography
                      component="span"
                      aria-hidden
                      sx={{
                        fontSize: "0.75rem",
                        fontWeight: 700,
                        color: w.trend === "up" ? "success.main" : w.trend === "down" ? "error.main" : "text.secondary",
                      }}
                    >
                      {glyph}
                      {w.comparison && w.comparison.includes("%") ? ` ${w.comparison.match(/[+-]?\d+%/)?.[0] ?? ""}` : ""}
                    </Typography>
                  )}
                </Stack>

                {/* Bottom: Subtitle + trend context + action hint */}
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: 600,
                    lineHeight: 1.2,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                  }}
                >
                  {w.pinned && (
                    <PushPinIcon sx={{ fontSize: 11, mr: 0.3, verticalAlign: "middle" }} color="primary" aria-label="Pinned" />
                  )}
                  {w.title}
                  {w.unit ? (
                    <Typography component="span" variant="caption" color="text.secondary" sx={{ ml: 0.4, fontWeight: 500 }}>
                      {w.unit}
                    </Typography>
                  ) : null}
                </Typography>

                {!executive && (w.explanation || w.tooltip) && (
                  <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{
                      lineHeight: 1.2,
                      display: "-webkit-box",
                      WebkitLineClamp: 1,
                      WebkitBoxOrient: "vertical",
                      overflow: "hidden",
                    }}
                  >
                    {w.explanation ?? w.tooltip}
                  </Typography>
                )}

                {w.path && (
                  <Typography
                    className="kpi-action"
                    variant="caption"
                    color="primary"
                    sx={{ opacity: 0, fontWeight: 600, mt: "auto", transition: "opacity 120ms ease" }}
                  >
                    Open
                  </Typography>
                )}

                {showTimestamps && w.lastUpdatedUtc && (
                  <Typography variant="caption" color="text.secondary" sx={{ fontSize: "0.65rem" }}>
                    {new Date(w.lastUpdatedUtc).toLocaleTimeString()}
                  </Typography>
                )}
              </Stack>
            </Paper>
          );

          return (
            <Box key={w.code}>
              {hoverDetail ? (
                <Tooltip title={hoverDetail} arrow>
                  <Box sx={{ height: "100%" }}>{card}</Box>
                </Tooltip>
              ) : (
                card
              )}
            </Box>
          );
        })}
      </Box>

      <Menu
        open={Boolean(menu)}
        anchorEl={menu?.anchor}
        onClose={() => setMenu(null)}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
      >
        <MenuItem
          onClick={() => {
            if (menu) openPath(menu.widget, "details");
            setMenu(null);
          }}
        >
          Open
        </MenuItem>
        <MenuItem
          onClick={() => {
            if (menu) openPath(menu.widget, "module");
            setMenu(null);
          }}
        >
          Open Module
        </MenuItem>
        <MenuItem
          disabled={!menu?.widget.reportPath}
          onClick={() => {
            if (menu) openPath(menu.widget, "report");
            setMenu(null);
          }}
        >
          Open Report
        </MenuItem>
        {onHelp && (
          <MenuItem
            onClick={() => {
              if (menu) openPath(menu.widget, "help");
              setMenu(null);
            }}
          >
            Help
          </MenuItem>
        )}
      </Menu>
    </>
  );
};

export const EmptyDashboardHint = ({ message }: { message: string }) => (
  <Alert severity="info" sx={{ mb: 2 }}>
    {message}
  </Alert>
);
