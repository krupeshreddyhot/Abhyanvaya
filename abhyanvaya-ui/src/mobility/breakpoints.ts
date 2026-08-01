/**
 * AI22.7C Phase 1 — Enterprise mobility breakpoints & device helpers.
 * Presentation only — never drives backend / recognition logic.
 */

/** Spec: width < 768px → Mobile Capture Mode. */
export const MOBILE_MAX_WIDTH_PX = 767;

/** Tablet band (inclusive): typically 768–1366 on the short/long side. */
export const TABLET_MIN_WIDTH_PX = 768;
export const TABLET_MAX_WIDTH_PX = 1366;

export type MobilitySurface = "phone" | "tablet" | "desktop";

export function resolveMobilitySurface(width: number, height: number): MobilitySurface {
  if (width <= MOBILE_MAX_WIDTH_PX) {
    return "phone";
  }
  const minSide = Math.min(width, height);
  const maxSide = Math.max(width, height);
  if (minSide >= 600 && maxSide <= TABLET_MAX_WIDTH_PX) {
    return "tablet";
  }
  if (width < 1024) {
    return "tablet";
  }
  return "desktop";
}

export function isMobileCaptureWidth(width: number): boolean {
  return width <= MOBILE_MAX_WIDTH_PX;
}

export function isTabletReviewWidth(width: number): boolean {
  return width >= TABLET_MIN_WIDTH_PX && width <= TABLET_MAX_WIDTH_PX;
}

/** CSS env() safe-area insets for notched / foldable devices. */
export const safeAreaSx = {
  pb: "max(12px, env(safe-area-inset-bottom))",
  pt: "max(0px, env(safe-area-inset-top))",
  pl: "max(0px, env(safe-area-inset-left))",
  pr: "max(0px, env(safe-area-inset-right))",
} as const;

export const MOBILE_TOUCH_TARGET_PX = 48;
export const TABLET_TOUCH_TARGET_PX = 44;
