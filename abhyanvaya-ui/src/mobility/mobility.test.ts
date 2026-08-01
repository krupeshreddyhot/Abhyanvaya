import { describe, expect, it, vi } from "vitest";
import {
  clampScale,
  createLongPressController,
  isDoubleTap,
  isMobileCaptureWidth,
  isTabletReviewWidth,
  MOBILITY_PHASE2_EXTENSIONS,
  pinchScaleFactor,
  resolveMobilitySurface,
  resolveSwipe,
  touchDistance,
} from "./index";
import { buildCaptureGuidance } from "./SmartCaptureAssistant";
import {
  adaptiveImageQuality,
  detectDeviceCapability,
  shouldVirtualizeList,
} from "./devicePerformance";
import { isPhase2CapabilityEnabled } from "./extensionPoints";

describe("AI22.7C mobility breakpoints", () => {
  it("switches to Mobile Capture Mode below 768px", () => {
    expect(isMobileCaptureWidth(767)).toBe(true);
    expect(isMobileCaptureWidth(768)).toBe(false);
  });

  it("resolves phone / tablet / desktop surfaces", () => {
    expect(resolveMobilitySurface(390, 844)).toBe("phone");
    expect(resolveMobilitySurface(820, 1180)).toBe("tablet");
    expect(resolveMobilitySurface(1440, 900)).toBe("desktop");
  });

  it("detects tablet review width band", () => {
    expect(isTabletReviewWidth(1024)).toBe(true);
    expect(isTabletReviewWidth(400)).toBe(false);
  });
});

describe("AI22.7C gestures", () => {
  it("computes pinch scale factor and clamps scale", () => {
    expect(pinchScaleFactor(100, 150)).toBeCloseTo(1.5);
    expect(pinchScaleFactor(0, 150)).toBe(1);
    expect(clampScale(12)).toBe(8);
    expect(clampScale(0.1)).toBe(0.25);
  });

  it("detects double tap within threshold", () => {
    expect(isDoubleTap(1000, 1200)).toBe(true);
    expect(isDoubleTap(1000, 1400)).toBe(false);
    expect(isDoubleTap(null, 1000)).toBe(false);
  });

  it("resolves swipe directions for next/previous image", () => {
    expect(resolveSwipe(200, 100, 40, 110)).toBe("left");
    expect(resolveSwipe(40, 100, 200, 110)).toBe("right");
  });

  it("measures touch distance", () => {
    expect(touchDistance({ clientX: 0, clientY: 0 }, { clientX: 3, clientY: 4 })).toBe(5);
  });

  it("fires long-press after delay and cancels", () => {
    vi.useFakeTimers();
    const controller = createLongPressController(500);
    const fire = vi.fn();
    controller.start(fire);
    vi.advanceTimersByTime(499);
    expect(fire).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(fire).toHaveBeenCalledTimes(1);

    const cancelled = vi.fn();
    controller.start(cancelled);
    controller.cancel();
    vi.advanceTimersByTime(600);
    expect(cancelled).not.toHaveBeenCalled();
    vi.useRealTimers();
  });
});

describe("AI22.7C smart capture assistant", () => {
  it("emits guidance without blocking (tips only)", () => {
    const tips = buildCaptureGuidance({
      lighting: "dark",
      blurScore: 10,
      stability: "shaking",
      framing: "left",
      distance: "too-far",
      estimatedFaces: 0,
    });
    expect(tips.some((t) => t.includes("dark"))).toBe(true);
    expect(tips.some((t) => t.includes("blurry") || t.includes("steady"))).toBe(true);
    expect(tips.some((t) => t.includes("left"))).toBe(true);
  });

  it("returns empty guidance when signals are healthy", () => {
    expect(buildCaptureGuidance({ lighting: "ok", blurScore: 500, stability: "ok" })).toEqual([]);
  });
});

describe("AI22.7C device performance", () => {
  it("flags low-memory / low-bandwidth profiles", () => {
    const profile = detectDeviceCapability({
      deviceMemory: 2,
      hardwareConcurrency: 2,
      connection: { saveData: true, effectiveType: "2g" },
    });
    expect(profile.lowMemory).toBe(true);
    expect(profile.lowBandwidth).toBe(true);
    expect(adaptiveImageQuality(profile)).toBe("low");
    expect(shouldVirtualizeList(15, profile)).toBe(true);
  });

  it("keeps Phase 2 capabilities disabled", () => {
    expect(MOBILITY_PHASE2_EXTENSIONS.offlineMode).toBe(false);
    expect(isPhase2CapabilityEnabled("pwa-install")).toBe(false);
  });
});
