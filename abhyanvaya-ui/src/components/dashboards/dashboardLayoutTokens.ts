/** AI31.8.2 — presentation-only layout tokens (no business logic). */

export const fluidDashboardSx = {
  width: "100%",
  mx: "auto",
  px: { xs: 1, sm: 1.25, md: 1.75 },
  maxWidth: {
    xs: "100%",
    sm: "100%",
    md: 1320,
    lg: 1500,
    xl: 1750,
  },
  // Card size CSS variables — denser than AI31.8.1A (~25–30% shorter hero cards)
  "--dash-card-sm": "112px",
  "--dash-card-md": "160px",
  "--dash-card-lg": "240px",
  "--dash-gap": "8px",
  "--dash-context-max-h": "70px",
} as const;

/** Subtle section accent colors (border / header tint only). */
export const sectionAccent: Record<string, { border: string; tint: string }> = {
  executive: { border: "#1976d2", tint: "rgba(25, 118, 210, 0.05)" },
  context: { border: "#546e7a", tint: "rgba(84, 110, 122, 0.04)" },
  brief: { border: "#1565c0", tint: "rgba(21, 101, 192, 0.05)" },
  attention: { border: "#d32f2f", tint: "rgba(211, 47, 47, 0.05)" },
  today: { border: "#3949ab", tint: "rgba(57, 73, 171, 0.05)" },
  scheduling: { border: "#7b1fa2", tint: "rgba(123, 31, 162, 0.05)" },
  attendance: { border: "#2e7d32", tint: "rgba(46, 125, 50, 0.05)" },
  academic: { border: "#00897b", tint: "rgba(0, 137, 123, 0.05)" },
  health: { border: "#616161", tint: "rgba(97, 97, 97, 0.05)" },
  timeline: { border: "#3949ab", tint: "rgba(57, 73, 171, 0.04)" },
  visualizations: { border: "#455a64", tint: "rgba(69, 90, 100, 0.05)" },
  actions: { border: "#546e7a", tint: "rgba(84, 110, 122, 0.05)" },
};

export const denseKpiColumns = {
  xs: 1,
  sm: 2,
  md: 4,
  lg: 4,
  xl: 4,
} as const;

export const standardKpiColumns = {
  xs: 1,
  sm: 2,
  md: 3,
  lg: 4,
  xl: 4,
} as const;

export const trendGlyph = (trend?: string | null) => {
  if (trend === "up") return "▲";
  if (trend === "down") return "▼";
  if (trend === "flat") return "➜";
  return null;
};

/** @deprecated AI31.8.1A hero codes — replaced by operational composition in AI31.8.2 */
export const HERO_SUMMARY_CODES = [
  "exec-critical-alerts",
  "exec-classes-running",
  "exec-attendance-completion",
  "exec-pending-reviews",
  "exec-faculty-teaching",
  "exec-scheduled-classes",
  "exec-platform-health",
  "exec-attendance-recorded",
] as const;

export const severityRank = (status?: string | null) => {
  if (status === "Red") return 0;
  if (status === "Orange") return 1;
  if (status === "Yellow") return 2;
  if (status === "Info") return 3;
  return 4;
};
