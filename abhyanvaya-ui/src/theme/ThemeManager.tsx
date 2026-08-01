import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { CssBaseline, GlobalStyles, ThemeProvider } from "@mui/material";
import { createEnterpriseTheme } from "./createEnterpriseTheme";
import type { ThemeModePreference, WorkspaceDensity } from "./enterpriseTokens";
import {
  applyResolvedSchemeToDocument,
  resolveInitialTheme,
} from "./resolveInitialTheme";
import {
  applyWorkspaceProfile,
  loadEnterpriseWorkspacePrefs,
  saveEnterpriseWorkspacePrefs,
  type EnterpriseWorkspacePrefs,
  type WorkspaceProfileId,
} from "./workspacePersonalization";

// themeAugmentation.d.ts is ambient (TypeScript-only) — do not import it at runtime (Vite cannot resolve .d.ts).

type ThemeManagerContextValue = {
  prefs: EnterpriseWorkspacePrefs;
  themeMode: ThemeModePreference;
  resolvedScheme: "light" | "dark" | "highContrast";
  density: WorkspaceDensity;
  setThemeMode: (mode: ThemeModePreference) => void;
  setDensity: (density: WorkspaceDensity) => void;
  setProfile: (profile: WorkspaceProfileId) => void;
  updatePrefs: (patch: Partial<EnterpriseWorkspacePrefs>) => void;
  cycleThemeMode: () => void;
};

const ThemeManagerContext = createContext<ThemeManagerContextValue | null>(null);

const THEME_CYCLE: ThemeModePreference[] = ["light", "dark", "system", "highContrast"];

/**
 * AI22.7B / R1 — Enterprise ThemeManager.
 * Application default is Light. System preference is consulted only when mode === "system".
 */
export function ThemeManager({ children }: { children: ReactNode }) {
  const [prefs, setPrefs] = useState<EnterpriseWorkspacePrefs>(() => loadEnterpriseWorkspacePrefs());
  const [systemPrefersDark, setSystemPrefersDark] = useState(false);

  // AI22.7B-R1 Step 4 — never call prefers-color-scheme unless Theme == System.
  useEffect(() => {
    if (prefs.themeMode !== "system") {
      setSystemPrefersDark(false);
      return;
    }
    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    setSystemPrefersDark(mq.matches);
    const onChange = () => setSystemPrefersDark(mq.matches);
    mq.addEventListener("change", onChange);
    return () => mq.removeEventListener("change", onChange);
  }, [prefs.themeMode]);

  const resolvedScheme = useMemo(() => {
    // prefs.themeMode is already normalized by loadEnterpriseWorkspacePrefs (R1).
    return resolveInitialTheme({
      savedThemeMode: prefs.themeMode,
      appearanceSelected: true,
      systemPrefersDark: prefs.themeMode === "system" ? systemPrefersDark : false,
    }).resolvedScheme;
  }, [prefs.themeMode, systemPrefersDark]);

  const density: WorkspaceDensity = prefs.largeFonts
    ? prefs.density === "compact"
      ? "standard"
      : prefs.density === "standard"
        ? "largeMonitor"
        : prefs.density
    : prefs.density;

  const theme = useMemo(
    () => createEnterpriseTheme(resolvedScheme, density),
    [resolvedScheme, density],
  );

  useEffect(() => {
    applyResolvedSchemeToDocument(resolvedScheme);
    document.documentElement.dataset.workspaceDensity = density;
    document.documentElement.dataset.workspaceProfile = prefs.profile;
  }, [resolvedScheme, density, prefs.profile]);

  const updatePrefs = useCallback((patch: Partial<EnterpriseWorkspacePrefs>) => {
    setPrefs(saveEnterpriseWorkspacePrefs(patch));
  }, []);

  const setThemeMode = useCallback(
    (mode: ThemeModePreference) => {
      // Appearance selection is user-explicit and survives logout (storage not cleared).
      updatePrefs({ themeMode: mode, appearanceSelected: true });
    },
    [updatePrefs],
  );

  const setDensity = useCallback(
    (next: WorkspaceDensity) => {
      updatePrefs({ density: next, profile: next });
    },
    [updatePrefs],
  );

  const setProfile = useCallback((profile: WorkspaceProfileId) => {
    setPrefs(applyWorkspaceProfile(profile));
  }, []);

  const cycleThemeMode = useCallback(() => {
    const index = THEME_CYCLE.indexOf(prefs.themeMode);
    const next = THEME_CYCLE[(index + 1) % THEME_CYCLE.length];
    updatePrefs({ themeMode: next, appearanceSelected: true });
  }, [prefs.themeMode, updatePrefs]);

  const value = useMemo<ThemeManagerContextValue>(
    () => ({
      prefs,
      themeMode: prefs.themeMode,
      resolvedScheme,
      density,
      setThemeMode,
      setDensity,
      setProfile,
      updatePrefs,
      cycleThemeMode,
    }),
    [
      prefs,
      resolvedScheme,
      density,
      setThemeMode,
      setDensity,
      setProfile,
      updatePrefs,
      cycleThemeMode,
    ],
  );

  return (
    <ThemeManagerContext.Provider value={value}>
      <ThemeProvider theme={theme}>
        <CssBaseline enableColorScheme />
        <GlobalStyles
          styles={{
            "@keyframes enterprise-highlight-pulse": {
              "0%": { boxShadow: "0 0 0 0 rgba(25, 118, 210, 0.55)" },
              "70%": { boxShadow: "0 0 0 8px rgba(25, 118, 210, 0)" },
              "100%": { boxShadow: "0 0 0 0 rgba(25, 118, 210, 0)" },
            },
            "@keyframes enterprise-fade-in": {
              from: { opacity: 0, transform: "translateY(4px)" },
              to: { opacity: 1, transform: "translateY(0)" },
            },
          }}
        />
        {children}
      </ThemeProvider>
    </ThemeManagerContext.Provider>
  );
}

export function useThemeManager(): ThemeManagerContextValue {
  const ctx = useContext(ThemeManagerContext);
  if (!ctx) {
    throw new Error("useThemeManager must be used within ThemeManager");
  }
  return ctx;
}

/** Safe hook for optional chrome (e.g. login page outside manager — should not happen). */
export function useThemeManagerOptional(): ThemeManagerContextValue | null {
  return useContext(ThemeManagerContext);
}
