/**
 * AI29.1D Prompt 17 — academic operational UI tokens.
 * Reuses AI31.8.2 dashboard layout + enterprise theme; no parallel design system.
 */
import { sectionAccent, fluidDashboardSx } from "../dashboards/dashboardLayoutTokens";
import { TOUCH_TARGET_PX } from "../../theme/tabletExperience";

export { fluidDashboardSx, sectionAccent, TOUCH_TARGET_PX };

export type AcademicUiAccent = keyof typeof sectionAccent;

export const academicPageShellSx = {
  ...fluidDashboardSx,
  py: { xs: 1, sm: 1.25, md: 1.5 },
  pb: { xs: "calc(16px + env(safe-area-inset-bottom))", md: 2 },
} as const;

/** Sticky scope / filter toolbar — matches AI31 densified dashboard toolbar. */
export const academicToolbarPaperSx = {
  position: "sticky",
  top: 0,
  zIndex: "appBar",
  p: { xs: 1, sm: 1.25 },
  mb: 1.25,
  border: "1px solid",
  borderColor: "divider",
  bgcolor: "background.paper",
} as const;

export const academicPanelSx = (accent: AcademicUiAccent = "academic") => {
  const a = sectionAccent[accent] ?? sectionAccent.academic;
  return {
    border: "1px solid",
    borderColor: "divider",
    borderLeft: `4px solid ${a.border}`,
    bgcolor: a.tint,
    p: { xs: 1, sm: 1.25 },
    mb: 1.25,
    overflow: "hidden",
  } as const;
};

/** Compact enterprise status chip (AI31 KPI chip density). */
export const academicChipSx = {
  height: 22,
  "& .MuiChip-label": { px: 0.75, fontSize: "0.7rem", fontWeight: 600 },
} as const;

export const academicTouchButtonSx = {
  minHeight: { xs: TOUCH_TARGET_PX, md: 34 },
  minWidth: { xs: TOUCH_TARGET_PX, md: "auto" },
} as const;

/** Map lifecycle / readiness / allocation labels to MUI chip colors. */
export function academicStatusChipColor(
  status?: string | null,
): "default" | "success" | "warning" | "error" | "info" | "primary" {
  const s = (status ?? "").toLowerCase();
  if (!s) return "default";
  if (
    s.includes("ready") ||
    s.includes("healthy") ||
    s.includes("active") ||
    s.includes("success") ||
    s.includes("complete") ||
    s.includes("published") ||
    s.includes("current")
  ) {
    return "success";
  }
  if (s.includes("warn") || s.includes("pending") || s.includes("draft") || s.includes("partial")) {
    return "warning";
  }
  if (
    s.includes("block") ||
    s.includes("critical") ||
    s.includes("error") ||
    s.includes("fail") ||
    s.includes("missed") ||
    s.includes("inactive") ||
    s.includes("deleted")
  ) {
    return "error";
  }
  if (s.includes("info") || s.includes("combined") || s.includes("locked")) return "info";
  return "default";
}
