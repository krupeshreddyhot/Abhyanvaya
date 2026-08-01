/**
 * AI22.7C Phase 1.7 — device / performance heuristics (presentation only).
 */

export type DeviceCapabilityProfile = {
  lowMemory: boolean;
  lowBandwidth: boolean;
  slowCpu: boolean;
  preferLiteImages: boolean;
  pauseHiddenVideo: boolean;
};

type NavigatorCapabilitySource = {
  deviceMemory?: number;
  hardwareConcurrency?: number;
  connection?: { saveData?: boolean; effectiveType?: string };
};

export function detectDeviceCapability(
  nav: NavigatorCapabilitySource = typeof navigator !== "undefined" ? navigator : {},
): DeviceCapabilityProfile {
  const deviceMemory = typeof nav.deviceMemory === "number" ? nav.deviceMemory : 8;
  const cores = typeof nav.hardwareConcurrency === "number" ? nav.hardwareConcurrency : 8;
  const connection = nav.connection;
  const effectiveType = connection?.effectiveType ?? "4g";
  const saveData = Boolean(connection?.saveData);

  const lowMemory = deviceMemory > 0 && deviceMemory <= 2;
  const slowCpu = cores > 0 && cores <= 4;
  const lowBandwidth =
    saveData || effectiveType === "slow-2g" || effectiveType === "2g" || effectiveType === "3g";

  return {
    lowMemory,
    lowBandwidth,
    slowCpu,
    preferLiteImages: lowMemory || lowBandwidth,
    pauseHiddenVideo: true,
  };
}

/** Adaptive gallery quality hint — consumers may downscale previews only. */
export function adaptiveImageQuality(profile: DeviceCapabilityProfile): "high" | "medium" | "low" {
  if (profile.preferLiteImages) {
    return "low";
  }
  if (profile.slowCpu) {
    return "medium";
  }
  return "high";
}

export function shouldVirtualizeList(itemCount: number, profile: DeviceCapabilityProfile): boolean {
  if (profile.lowMemory) {
    return itemCount > 12;
  }
  return itemCount > 30;
}
