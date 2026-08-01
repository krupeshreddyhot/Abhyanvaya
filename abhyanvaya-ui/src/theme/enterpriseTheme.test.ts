import { describe, expect, it, beforeEach, vi } from "vitest";
import { createEnterpriseTheme, resolveColorScheme } from "./createEnterpriseTheme";
import {
  brandColors,
  enterpriseMotion,
  enterpriseSpacing,
  recognitionColorTokens,
} from "./enterpriseTokens";
import {
  formatAccessibilityReport,
  runAccessibilityChecker,
} from "./accessibilityChecker";
import { isLandscape, isTabletViewport, resolveSwipe } from "./tabletExperience";
import {
  APPLICATION_DEFAULT_THEME_MODE,
  resolveInitialTheme,
  resolveThemeModePreference,
} from "./resolveInitialTheme";
import {
  applyWorkspaceProfile,
  loadEnterpriseWorkspacePrefs,
  saveEnterpriseWorkspacePrefs,
} from "./workspacePersonalization";
import { summarizeVisualAudit, VISUAL_COMPONENT_AUDIT } from "./visualConsistencyAudit";

describe("resolveInitialTheme (AI22.7B-R1)", () => {
  it("defaults to Light when no preference is saved", () => {
    expect(APPLICATION_DEFAULT_THEME_MODE).toBe("light");
    expect(resolveInitialTheme({})).toEqual({ themeMode: "light", resolvedScheme: "light" });
    expect(resolveThemeModePreference({})).toBe("light");
  });

  it("ignores polluted system default without appearanceSelected", () => {
    expect(
      resolveInitialTheme({
        savedThemeMode: "system",
        appearanceSelected: false,
        systemPrefersDark: true,
      }),
    ).toEqual({ themeMode: "light", resolvedScheme: "light" });
  });

  it("uses System + OS only when user selected System", () => {
    expect(
      resolveInitialTheme({
        savedThemeMode: "system",
        appearanceSelected: true,
        systemPrefersDark: true,
      }),
    ).toEqual({ themeMode: "system", resolvedScheme: "dark" });
    expect(
      resolveInitialTheme({
        savedThemeMode: "system",
        appearanceSelected: true,
        systemPrefersDark: false,
      }),
    ).toEqual({ themeMode: "system", resolvedScheme: "light" });
  });

  it("honors explicit Dark and High Contrast", () => {
    expect(
      resolveInitialTheme({ savedThemeMode: "dark", appearanceSelected: true }),
    ).toEqual({ themeMode: "dark", resolvedScheme: "dark" });
    expect(
      resolveInitialTheme({ savedThemeMode: "highContrast", appearanceSelected: true }),
    ).toEqual({ themeMode: "highContrast", resolvedScheme: "highContrast" });
  });

  it("does not apply OS dark when preference is Light", () => {
    expect(
      resolveInitialTheme({
        savedThemeMode: "light",
        appearanceSelected: true,
        systemPrefersDark: true,
      }),
    ).toEqual({ themeMode: "light", resolvedScheme: "light" });
  });
});

describe("createEnterpriseTheme (AI22.7B 5.1)", () => {
  it("builds light, dark, and high-contrast themes with recognition tokens", () => {
    const light = createEnterpriseTheme("light");
    const dark = createEnterpriseTheme("dark");
    const hc = createEnterpriseTheme("highContrast");

    expect(light.palette.mode).toBe("light");
    expect(dark.palette.mode).toBe("dark");
    expect(hc.palette.background.default).toBe("#000000");
    expect(light.palette.recognition.confidenceExcellent).toBe(
      recognitionColorTokens.confidence.excellent,
    );
    expect(hc.palette.recognition.confidenceExcellent).toBe(
      recognitionColorTokens.confidence.cbFriendly.excellent,
    );
    expect(light.enterprise.spacing.md).toBe(enterpriseSpacing.md);
    expect(light.transitions.duration.standard).toBe(enterpriseMotion.standard);
    expect(light.palette.primary.main).toBe(brandColors.primary.main);
  });

  it("resolves system preference", () => {
    expect(resolveColorScheme("system", true)).toBe("dark");
    expect(resolveColorScheme("system", false)).toBe("light");
    expect(resolveColorScheme("highContrast", false)).toBe("highContrast");
  });
});

describe("workspacePersonalization (AI22.7B 5.8)", () => {
  beforeEach(() => {
    const store = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => {
        store.set(key, value);
      },
      removeItem: (key: string) => {
        store.delete(key);
      },
      clear: () => store.clear(),
    });
  });

  it("persists theme mode and panel sizes", () => {
    saveEnterpriseWorkspacePrefs({
      themeMode: "dark",
      appearanceSelected: true,
      photoFlex: 32,
      listFlex: 36,
    });
    const prefs = loadEnterpriseWorkspacePrefs();
    expect(prefs.themeMode).toBe("dark");
    expect(prefs.appearanceSelected).toBe(true);
    expect(prefs.photoFlex).toBe(32);
    expect(prefs.listFlex).toBe(36);
  });

  it("first load without preference resolves to Light (R1)", () => {
    const prefs = loadEnterpriseWorkspacePrefs();
    expect(prefs.themeMode).toBe("light");
    expect(prefs.appearanceSelected).toBe(false);
  });

  it("migrates polluted system default to Light", () => {
    localStorage.setItem(
      "abhyanvaya.enterpriseWorkspace.v1",
      JSON.stringify({ themeMode: "system", appearanceSelected: false }),
    );
    const prefs = loadEnterpriseWorkspacePrefs();
    expect(prefs.themeMode).toBe("light");
  });

  it("applies workspace profiles", () => {
    const touch = applyWorkspaceProfile("touch");
    expect(touch.profile).toBe("touch");
    expect(touch.density).toBe("touch");
    expect(touch.filmstripHeight).toBeGreaterThanOrEqual(100);
  });
});

describe("tabletExperience (AI22.7B 5.4)", () => {
  it("detects tablet viewport and swipe directions", () => {
    expect(isTabletViewport(1024, 768)).toBe(true);
    expect(isTabletViewport(390, 844)).toBe(false);
    expect(isLandscape(1024, 768)).toBe(true);
    expect(resolveSwipe(0, 0, 80, 10)).toBe("right");
    expect(resolveSwipe(80, 0, 0, 10)).toBe("left");
  });
});

describe("accessibilityChecker (AI22.7B 5.3)", () => {
  it("reports missing alt and formats output", () => {
    document.body.innerHTML = `<main><img src="x.png" /><button></button></main>`;
    const report = runAccessibilityChecker(document);
    expect(report.errors).toBeGreaterThan(0);
    expect(formatAccessibilityReport(report)).toContain("Accessibility Report");
  });
});

describe("visualConsistencyAudit (AI22.7B 5.7)", () => {
  it("summarizes component audit", () => {
    const summary = summarizeVisualAudit(VISUAL_COMPONENT_AUDIT);
    expect(summary.total).toBe(VISUAL_COMPONENT_AUDIT.length);
    expect(summary.aligned).toBeGreaterThan(0);
  });
});
