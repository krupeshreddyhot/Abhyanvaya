/**
 * AI22.7B Phase 5.4 — tablet / touch helpers (no workflow changes).
 */

export type PointerModality = "mouse" | "touch" | "pen" | "unknown";

export function detectPointerModality(event?: PointerEvent | React.PointerEvent): PointerModality {
  const type = event?.pointerType;
  if (type === "touch" || type === "pen" || type === "mouse") {
    return type;
  }
  if (typeof window === "undefined") {
    return "unknown";
  }
  if (window.matchMedia("(pointer: coarse)").matches) {
    return "touch";
  }
  return "mouse";
}

export function isTabletViewport(width: number, height: number): boolean {
  const minSide = Math.min(width, height);
  const maxSide = Math.max(width, height);
  return minSide >= 600 && maxSide <= 1366;
}

export function isLandscape(width: number, height: number): boolean {
  return width >= height;
}

/** Long-press threshold for stylus / finger secondary actions. */
export const LONG_PRESS_MS = 500;

/** Minimum touch target (WCAG 2.5.5 / Apple HIG aligned). */
export const TOUCH_TARGET_PX = 44;

export type SwipeDirection = "left" | "right" | "up" | "down" | null;

export function resolveSwipe(
  startX: number,
  startY: number,
  endX: number,
  endY: number,
  threshold = 48,
): SwipeDirection {
  const dx = endX - startX;
  const dy = endY - startY;
  if (Math.abs(dx) < threshold && Math.abs(dy) < threshold) {
    return null;
  }
  if (Math.abs(dx) > Math.abs(dy)) {
    return dx > 0 ? "right" : "left";
  }
  return dy > 0 ? "down" : "up";
}
