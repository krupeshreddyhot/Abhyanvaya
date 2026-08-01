/**
 * AI22.7B Phase 5.1 / 5.7 — Enterprise visual design tokens.
 * Consumed by ThemeManager; do not hardcode these values in page layouts.
 */

export const enterpriseSpacing = {
  none: 0,
  xxs: 2,
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
} as const;

export const enterpriseRadius = {
  none: 0,
  sm: 4,
  md: 8,
  lg: 12,
  pill: 999,
} as const;

export const enterpriseElevation = {
  flat: 0,
  raised: 1,
  overlay: 4,
  modal: 8,
  popover: 12,
} as const;

export const enterpriseShadows = {
  light: {
    none: "none",
    sm: "0 1px 2px rgba(15, 23, 42, 0.08)",
    md: "0 4px 12px rgba(15, 23, 42, 0.12)",
    lg: "0 12px 28px rgba(15, 23, 42, 0.16)",
  },
  dark: {
    none: "none",
    sm: "0 1px 2px rgba(0, 0, 0, 0.45)",
    md: "0 4px 14px rgba(0, 0, 0, 0.55)",
    lg: "0 14px 32px rgba(0, 0, 0, 0.65)",
  },
} as const;

export const enterpriseTypography = {
  fontFamily: '"Segoe UI", "Roboto", "Helvetica", "Arial", sans-serif',
  fontSize: 14,
  h1: { fontSize: "2rem", fontWeight: 700, lineHeight: 1.25 },
  h2: { fontSize: "1.5rem", fontWeight: 700, lineHeight: 1.3 },
  h3: { fontSize: "1.25rem", fontWeight: 600, lineHeight: 1.35 },
  h4: { fontSize: "1.125rem", fontWeight: 600, lineHeight: 1.4 },
  body: { fontSize: "0.875rem", fontWeight: 400, lineHeight: 1.5 },
  caption: { fontSize: "0.75rem", fontWeight: 400, lineHeight: 1.4 },
  mono: '"Cascadia Code", "Consolas", "Courier New", monospace',
} as const;

/** Material Motion–aligned durations (ms). */
export const enterpriseMotion = {
  shortest: 100,
  shorter: 150,
  short: 200,
  standard: 250,
  complex: 325,
  enteringScreen: 225,
  leavingScreen: 195,
  easing: {
    easeInOut: "cubic-bezier(0.4, 0, 0.2, 1)",
    easeOut: "cubic-bezier(0.0, 0, 0.2, 1)",
    easeIn: "cubic-bezier(0.4, 0, 1, 1)",
    sharp: "cubic-bezier(0.4, 0, 0.6, 1)",
  },
} as const;

export type SemanticTone = {
  main: string;
  light: string;
  dark: string;
  contrastText: string;
};

export const semanticColors = {
  success: { main: "#2e7d32", light: "#4caf50", dark: "#1b5e20", contrastText: "#ffffff" },
  warning: { main: "#ed6c02", light: "#ff9800", dark: "#e65100", contrastText: "#ffffff" },
  error: { main: "#d32f2f", light: "#ef5350", dark: "#c62828", contrastText: "#ffffff" },
  info: { main: "#0288d1", light: "#03a9f4", dark: "#01579b", contrastText: "#ffffff" },
} as const satisfies Record<string, SemanticTone>;

/** Recognition / confidence / image-status / gallery / toolbar domain colors. */
export const recognitionColorTokens = {
  confidence: {
    excellent: "#2e7d32",
    high: "#2e7d32",
    medium: "#f9a825",
    low: "#ef6c00",
    unknown: "#9e9e9e",
    /** Color-blind friendly secondary cues (pattern-friendly accents). */
    cbFriendly: {
      excellent: "#0072B2",
      high: "#56B4E9",
      medium: "#E69F00",
      low: "#D55E00",
      unknown: "#999999",
    },
  },
  imageStatus: {
    uploaded: "#0288d1",
    processing: "#ed6c02",
    ready: "#2e7d32",
    needsReview: "#0288d1",
    error: "#d32f2f",
    waiting: "#757575",
  },
  gallery: {
    selectedBorder: "#1976d2",
    hoverOverlay: "rgba(25, 118, 210, 0.12)",
    canvasBackground: "#121212",
  },
  toolbar: {
    stickyBackgroundLight: "#ffffff",
    stickyBackgroundDark: "#1e1e1e",
    accent: "#6A1B9A",
  },
  overlay: {
    /** Keep overlays readable on photos in both themes. */
    labelBackground: "rgba(0, 0, 0, 0.72)",
    labelText: "#ffffff",
    focusRing: "#ffffff",
  },
} as const;

export const brandColors = {
  primary: { main: "#1976d2", light: "#42a5f5", dark: "#1565c0", contrastText: "#ffffff" },
  secondary: { main: "#6A1B9A", light: "#9c4dcc", dark: "#4a0072", contrastText: "#ffffff" },
  aiAccent: "#6A1B9A",
} as const;

export type ThemeModePreference = "light" | "dark" | "system" | "highContrast";
export type ResolvedColorScheme = "light" | "dark" | "highContrast";

export type WorkspaceDensity = "compact" | "standard" | "largeMonitor" | "touch";

export const densityScale: Record<WorkspaceDensity, { fontScale: number; controlHeight: number; spacingFactor: number }> = {
  compact: { fontScale: 0.92, controlHeight: 32, spacingFactor: 0.85 },
  standard: { fontScale: 1, controlHeight: 36, spacingFactor: 1 },
  largeMonitor: { fontScale: 1.05, controlHeight: 40, spacingFactor: 1.1 },
  touch: { fontScale: 1.05, controlHeight: 44, spacingFactor: 1.15 },
};
