import type {} from "@mui/material/styles";

/**
 * AI22.7B Phase 5.1 — MUI theme augmentation for enterprise domain colors.
 */
declare module "@mui/material/styles" {
  interface Palette {
    recognition: {
      confidenceExcellent: string;
      confidenceHigh: string;
      confidenceMedium: string;
      confidenceLow: string;
      confidenceUnknown: string;
      imageUploaded: string;
      imageProcessing: string;
      imageReady: string;
      imageNeedsReview: string;
      imageError: string;
      gallerySelected: string;
      galleryHover: string;
      galleryCanvas: string;
      toolbarAccent: string;
      overlayLabelBg: string;
      overlayLabelText: string;
      aiAccent: string;
    };
  }

  interface PaletteOptions {
    recognition?: Partial<Palette["recognition"]>;
  }

  interface Theme {
    enterprise: {
      spacing: typeof import("./enterpriseTokens").enterpriseSpacing;
      radius: typeof import("./enterpriseTokens").enterpriseRadius;
      elevation: typeof import("./enterpriseTokens").enterpriseElevation;
      motion: typeof import("./enterpriseTokens").enterpriseMotion;
      density: import("./enterpriseTokens").WorkspaceDensity;
    };
  }

  interface ThemeOptions {
    enterprise?: Theme["enterprise"];
  }
}

export {};
