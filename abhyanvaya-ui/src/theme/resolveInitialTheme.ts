/**
 * AI22.7B-R1 — Enterprise theme initialization.
 *
 * Precedence:
 * 1. User theme preference (when explicitly selected)
 * 2. System theme — only when preference === "system"
 * 3. Application default — Light
 */

import type { ResolvedColorScheme, ThemeModePreference } from "./enterpriseTokens";

export const ENTERPRISE_WORKSPACE_STORAGE_KEY = "abhyanvaya.enterpriseWorkspace.v1";

export const APPLICATION_DEFAULT_THEME_MODE: ThemeModePreference = "light";

export type ThemeInitInput = {
  /** Raw preference from storage, if any. */
  savedThemeMode?: ThemeModePreference | null;
  /** True only after the user picks an Appearance option. */
  appearanceSelected?: boolean;
  /** Evaluated only when mode === "system". */
  systemPrefersDark?: boolean;
};

export type ThemeInitResult = {
  themeMode: ThemeModePreference;
  resolvedScheme: ResolvedColorScheme;
};

/** Read OS dark preference — call ONLY when themeMode === "system". */
export function readSystemPrefersDark(): boolean {
  if (typeof window === "undefined" || typeof window.matchMedia !== "function") {
    return false;
  }
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

/**
 * ResolveInitialTheme — never treats Dark as the application default.
 * Never consults prefers-color-scheme unless preference is System.
 */
export function resolveInitialTheme(input: ThemeInitInput = {}): ThemeInitResult {
  const themeMode = resolveThemeModePreference(input);

  if (themeMode === "highContrast") {
    return { themeMode, resolvedScheme: "highContrast" };
  }

  if (themeMode === "dark") {
    return { themeMode, resolvedScheme: "dark" };
  }

  if (themeMode === "system") {
    const systemPrefersDark =
      typeof input.systemPrefersDark === "boolean"
        ? input.systemPrefersDark
        : readSystemPrefersDark();
    return {
      themeMode,
      resolvedScheme: systemPrefersDark ? "dark" : "light",
    };
  }

  return { themeMode: "light", resolvedScheme: "light" };
}

export function resolveThemeModePreference(input: ThemeInitInput): ThemeModePreference {
  const saved = input.savedThemeMode;

  // User explicitly selected an Appearance option.
  if (input.appearanceSelected && isThemeMode(saved)) {
    return saved;
  }

  // Migration: Dark / High Contrast in storage implies prior explicit choice
  // (R1 default pollution only wrote "system").
  if (!input.appearanceSelected && (saved === "dark" || saved === "highContrast")) {
    return saved;
  }

  // No saved preference, or polluted default "system" without selection → Light.
  if (!saved || saved === "system") {
    return APPLICATION_DEFAULT_THEME_MODE;
  }

  if (isThemeMode(saved)) {
    return saved;
  }

  return APPLICATION_DEFAULT_THEME_MODE;
}

function isThemeMode(value: unknown): value is ThemeModePreference {
  return value === "light" || value === "dark" || value === "system" || value === "highContrast";
}

/** Apply resolved scheme to <html> before paint (shared with anti-flicker bootstrap). */
export function applyResolvedSchemeToDocument(scheme: ResolvedColorScheme): void {
  if (typeof document === "undefined") {
    return;
  }
  const root = document.documentElement;
  root.dataset.colorScheme = scheme;
  root.style.colorScheme = scheme === "light" ? "light" : "dark";
  if (scheme === "light") {
    root.style.backgroundColor = "#fafafa";
  } else if (scheme === "highContrast") {
    root.style.backgroundColor = "#000000";
  } else {
    root.style.backgroundColor = "#121212";
  }
}
