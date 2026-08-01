/**
 * AI22.7C Phase 1.3 — gesture helpers for the enterprise image viewer.
 * Mouse / keyboard behavior remains unchanged in the viewer hook.
 */

import { LONG_PRESS_MS, resolveSwipe, type SwipeDirection } from "../theme/tabletExperience";

export { LONG_PRESS_MS, resolveSwipe };
export type { SwipeDirection };

export const DOUBLE_TAP_MS = 280;
export const DOUBLE_TAP_ZOOM = 2;
export const PINCH_MIN_SCALE = 0.25;
export const PINCH_MAX_SCALE = 8;

export function pinchScaleFactor(previousDistance: number, nextDistance: number): number {
  if (previousDistance <= 0) {
    return 1;
  }
  return nextDistance / previousDistance;
}

export function clampScale(scale: number, min = PINCH_MIN_SCALE, max = PINCH_MAX_SCALE): number {
  return Math.min(max, Math.max(min, scale));
}

export function isDoubleTap(previousTs: number | null, nextTs: number, threshold = DOUBLE_TAP_MS): boolean {
  if (previousTs == null) {
    return false;
  }
  return nextTs - previousTs <= threshold;
}

export type LongPressControllers = {
  start: (onFire: () => void) => void;
  cancel: () => void;
};

export function createLongPressController(ms = LONG_PRESS_MS): LongPressControllers {
  let timer: ReturnType<typeof setTimeout> | null = null;
  return {
    start(onFire) {
      if (timer) {
        clearTimeout(timer);
      }
      timer = setTimeout(() => {
        timer = null;
        onFire();
      }, ms);
    },
    cancel() {
      if (timer) {
        clearTimeout(timer);
        timer = null;
      }
    },
  };
}

export function touchDistance(a: { clientX: number; clientY: number }, b: { clientX: number; clientY: number }): number {
  return Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
}
