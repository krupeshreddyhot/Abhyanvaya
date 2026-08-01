/** AI22.7B Phase 5.8 / R1 — workspace personalization profiles (localStorage only). */

import type { ThemeModePreference, WorkspaceDensity } from "./enterpriseTokens";
import {
  APPLICATION_DEFAULT_THEME_MODE,
  ENTERPRISE_WORKSPACE_STORAGE_KEY,
  resolveThemeModePreference,
} from "./resolveInitialTheme";

const KEY = ENTERPRISE_WORKSPACE_STORAGE_KEY;

export type WorkspaceProfileId = "compact" | "standard" | "largeMonitor" | "touch";

export type EnterpriseWorkspacePrefs = {
  themeMode: ThemeModePreference;
  /** AI22.7B-R1 — true only after user picks Appearance (Light/Dark/System/High Contrast). */
  appearanceSelected: boolean;
  density: WorkspaceDensity;
  profile: WorkspaceProfileId;
  fullscreen: boolean;
  photoFlex: number;
  listFlex: number;
  galleryMode: "grid" | "filmstrip" | "both";
  filmstripHeight: number;
  miniMapVisible: boolean;
  heatMapEnabled: boolean;
  heatMapOpacity: number;
  smartQueueOnlyPending: boolean;
  reducedMotionOverride: "system" | "reduce" | "no-preference";
  colorBlindFriendly: boolean;
  largeFonts: boolean;
};

const DEFAULTS: EnterpriseWorkspacePrefs = {
  themeMode: APPLICATION_DEFAULT_THEME_MODE,
  appearanceSelected: false,
  density: "standard",
  profile: "standard",
  fullscreen: false,
  photoFlex: 30,
  listFlex: 40,
  galleryMode: "both",
  filmstripHeight: 96,
  miniMapVisible: true,
  heatMapEnabled: false,
  heatMapOpacity: 0.35,
  smartQueueOnlyPending: true,
  reducedMotionOverride: "system",
  colorBlindFriendly: false,
  largeFonts: false,
};

const PROFILE_PRESETS: Record<WorkspaceProfileId, Partial<EnterpriseWorkspacePrefs>> = {
  compact: { density: "compact", filmstripHeight: 72, galleryMode: "filmstrip" },
  standard: { density: "standard", filmstripHeight: 96, galleryMode: "both" },
  largeMonitor: { density: "largeMonitor", filmstripHeight: 112, photoFlex: 34, listFlex: 38 },
  touch: { density: "touch", filmstripHeight: 120, galleryMode: "both", miniMapVisible: true },
};

function read(): EnterpriseWorkspacePrefs {
  if (typeof localStorage === "undefined") {
    return { ...DEFAULTS };
  }
  try {
    const raw = localStorage.getItem(KEY);
    if (!raw) {
      return { ...DEFAULTS };
    }
    const parsed = JSON.parse(raw) as Partial<EnterpriseWorkspacePrefs>;
    const appearanceSelected = Boolean(parsed.appearanceSelected);
    const themeMode = resolveThemeModePreference({
      savedThemeMode: parsed.themeMode,
      appearanceSelected,
    });
    // Persist migration: Dark/HC without flag counts as selected going forward.
    const migratedSelected =
      appearanceSelected || parsed.themeMode === "dark" || parsed.themeMode === "highContrast";

    return {
      ...DEFAULTS,
      ...parsed,
      themeMode,
      appearanceSelected: migratedSelected,
      photoFlex: clamp(parsed.photoFlex ?? DEFAULTS.photoFlex, 18, 46),
      listFlex: clamp(parsed.listFlex ?? DEFAULTS.listFlex, 22, 55),
      heatMapOpacity: clamp(parsed.heatMapOpacity ?? DEFAULTS.heatMapOpacity, 0.1, 0.85),
      filmstripHeight: clamp(parsed.filmstripHeight ?? DEFAULTS.filmstripHeight, 64, 160),
    };
  } catch {
    return { ...DEFAULTS };
  }
}

function write(prefs: EnterpriseWorkspacePrefs): void {
  if (typeof localStorage === "undefined") {
    return;
  }
  localStorage.setItem(KEY, JSON.stringify(prefs));
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

export function loadEnterpriseWorkspacePrefs(): EnterpriseWorkspacePrefs {
  return read();
}

export function saveEnterpriseWorkspacePrefs(
  patch: Partial<EnterpriseWorkspacePrefs>,
): EnterpriseWorkspacePrefs {
  const next = { ...read(), ...patch };
  write(next);
  return next;
}

export function applyWorkspaceProfile(profile: WorkspaceProfileId): EnterpriseWorkspacePrefs {
  const preset = PROFILE_PRESETS[profile] ?? {};
  return saveEnterpriseWorkspacePrefs({ ...preset, profile });
}

export function getProfilePreset(profile: WorkspaceProfileId): Partial<EnterpriseWorkspacePrefs> {
  return { ...PROFILE_PRESETS[profile] };
}

export const ENTERPRISE_WORKSPACE_DEFAULTS = DEFAULTS;
