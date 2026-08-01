import { createTheme, type Theme, type ThemeOptions } from "@mui/material/styles";
import {
  brandColors,
  densityScale,
  enterpriseElevation,
  enterpriseMotion,
  enterpriseRadius,
  enterpriseShadows,
  enterpriseSpacing,
  enterpriseTypography,
  recognitionColorTokens,
  semanticColors,
  type ResolvedColorScheme,
  type WorkspaceDensity,
} from "./enterpriseTokens";

function recognitionPalette(scheme: ResolvedColorScheme) {
  const conf =
    scheme === "highContrast"
      ? recognitionColorTokens.confidence.cbFriendly
      : recognitionColorTokens.confidence;

  return {
    confidenceExcellent: conf.excellent,
    confidenceHigh: conf.high,
    confidenceMedium: conf.medium,
    confidenceLow: conf.low,
    confidenceUnknown: conf.unknown,
    imageUploaded: recognitionColorTokens.imageStatus.uploaded,
    imageProcessing: recognitionColorTokens.imageStatus.processing,
    imageReady: recognitionColorTokens.imageStatus.ready,
    imageNeedsReview: recognitionColorTokens.imageStatus.needsReview,
    imageError: recognitionColorTokens.imageStatus.error,
    gallerySelected: recognitionColorTokens.gallery.selectedBorder,
    galleryHover: recognitionColorTokens.gallery.hoverOverlay,
    galleryCanvas: recognitionColorTokens.gallery.canvasBackground,
    toolbarAccent: recognitionColorTokens.toolbar.accent,
    overlayLabelBg: recognitionColorTokens.overlay.labelBackground,
    overlayLabelText: recognitionColorTokens.overlay.labelText,
    aiAccent: brandColors.aiAccent,
  };
}

function buildOptions(
  scheme: ResolvedColorScheme,
  density: WorkspaceDensity,
): ThemeOptions {
  const scale = densityScale[density];
  const isDark = scheme === "dark" || scheme === "highContrast";
  const shadows = isDark ? enterpriseShadows.dark : enterpriseShadows.light;

  const paletteMode = scheme === "highContrast" ? "dark" : scheme === "dark" ? "dark" : "light";

  return {
    palette: {
      mode: paletteMode,
      primary: brandColors.primary,
      secondary: brandColors.secondary,
      success: semanticColors.success,
      warning: semanticColors.warning,
      error: semanticColors.error,
      info: semanticColors.info,
      ...(scheme === "highContrast"
        ? {
            background: { default: "#000000", paper: "#0a0a0a" },
            text: { primary: "#ffffff", secondary: "#e0e0e0" },
            divider: "#ffffff",
          }
        : scheme === "dark"
          ? {
              background: { default: "#121212", paper: "#1e1e1e" },
              text: { primary: "#f5f5f5", secondary: "#bdbdbd" },
            }
          : {
              background: { default: "#fafafa", paper: "#ffffff" },
            }),
      recognition: recognitionPalette(scheme),
    },
    typography: {
      fontFamily: enterpriseTypography.fontFamily,
      fontSize: Math.round(enterpriseTypography.fontSize * scale.fontScale),
      h1: { ...enterpriseTypography.h1, fontSize: `calc(${enterpriseTypography.h1.fontSize} * ${scale.fontScale})` },
      h2: { ...enterpriseTypography.h2, fontSize: `calc(${enterpriseTypography.h2.fontSize} * ${scale.fontScale})` },
      h3: { ...enterpriseTypography.h3, fontSize: `calc(${enterpriseTypography.h3.fontSize} * ${scale.fontScale})` },
      h4: { ...enterpriseTypography.h4, fontSize: `calc(${enterpriseTypography.h4.fontSize} * ${scale.fontScale})` },
      body1: { fontSize: `${0.875 * scale.fontScale}rem` },
      body2: { fontSize: `${0.8125 * scale.fontScale}rem` },
      caption: { ...enterpriseTypography.caption, fontSize: `calc(${enterpriseTypography.caption.fontSize} * ${scale.fontScale})` },
      button: { textTransform: "none", fontWeight: 600 },
    },
    shape: { borderRadius: enterpriseRadius.md },
    spacing: (factor: number) => `${enterpriseSpacing.sm * scale.spacingFactor * factor}px`,
    transitions: {
      duration: {
        shortest: enterpriseMotion.shortest,
        shorter: enterpriseMotion.shorter,
        short: enterpriseMotion.short,
        standard: enterpriseMotion.standard,
        complex: enterpriseMotion.complex,
        enteringScreen: enterpriseMotion.enteringScreen,
        leavingScreen: enterpriseMotion.leavingScreen,
      },
      easing: {
        easeInOut: enterpriseMotion.easing.easeInOut,
        easeOut: enterpriseMotion.easing.easeOut,
        easeIn: enterpriseMotion.easing.easeIn,
        sharp: enterpriseMotion.easing.sharp,
      },
    },
    shadows: [
      "none",
      shadows.sm,
      shadows.sm,
      shadows.md,
      shadows.md,
      shadows.md,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
      shadows.lg,
    ] as Theme["shadows"],
    enterprise: {
      spacing: enterpriseSpacing,
      radius: enterpriseRadius,
      elevation: enterpriseElevation,
      motion: enterpriseMotion,
      density,
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          ":root": {
            colorScheme: isDark ? "dark" : "light",
          },
          "html": {
            // Large fonts / browser zoom friendly
            WebkitTextSizeAdjust: "100%",
          },
          "*:focus-visible": {
            outline: scheme === "highContrast" ? "3px solid #ffff00" : "2px solid #1976d2",
            outlineOffset: 2,
          },
          "@media (prefers-reduced-motion: reduce)": {
            "*, *::before, *::after": {
              animationDuration: "0.01ms !important",
              animationIterationCount: "1 !important",
              transitionDuration: "0.01ms !important",
              scrollBehavior: "auto !important",
            },
          },
          // Classroom / recognition images must not be inverted by theme.
          "img[data-enterprise-media='true'], .enterprise-media img": {
            filter: "none !important",
          },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: false },
        styleOverrides: {
          root: {
            minHeight: scale.controlHeight,
            borderRadius: enterpriseRadius.md,
          },
        },
      },
      MuiIconButton: {
        styleOverrides: {
          root: {
            // Touch-friendly hit area without redesigning icons
            minWidth: density === "touch" ? 44 : undefined,
            minHeight: density === "touch" ? 44 : undefined,
          },
        },
      },
      MuiChip: {
        styleOverrides: {
          root: { borderRadius: enterpriseRadius.sm },
        },
      },
      MuiPaper: {
        defaultProps: { elevation: enterpriseElevation.raised },
        styleOverrides: {
          root: { backgroundImage: "none" },
        },
      },
      MuiDialog: {
        styleOverrides: {
          paper: { borderRadius: enterpriseRadius.lg },
        },
      },
      MuiTooltip: {
        defaultProps: { arrow: true, enterTouchDelay: 400 },
      },
      MuiSkeleton: {
        defaultProps: { animation: "wave" },
      },
    },
  };
}

/** AI22.7B Phase 5.1 — builds Light / Dark / High Contrast MUI themes. */
export function createEnterpriseTheme(
  scheme: ResolvedColorScheme,
  density: WorkspaceDensity = "standard",
): Theme {
  return createTheme(buildOptions(scheme, density));
}

/** @deprecated Prefer resolveInitialTheme (AI22.7B-R1). Kept for callers/tests. */
export function resolveColorScheme(
  preference: "light" | "dark" | "system" | "highContrast",
  systemPrefersDark: boolean,
): ResolvedColorScheme {
  if (preference === "highContrast") {
    return "highContrast";
  }
  if (preference === "system") {
    return systemPrefersDark ? "dark" : "light";
  }
  if (preference === "dark") {
    return "dark";
  }
  return "light";
}
