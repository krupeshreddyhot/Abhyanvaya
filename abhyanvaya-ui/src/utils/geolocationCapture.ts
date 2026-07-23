export type CaptureGeoLocation = {
  latitude: number;
  longitude: number;
};

/**
 * Best-effort geolocation for capture metadata. Never blocks capture on denial/timeout.
 */
export const tryGetCaptureLocation = async (
  timeoutMs = 4000,
): Promise<CaptureGeoLocation | null> => {
  if (typeof navigator === "undefined" || !navigator.geolocation) {
    return null;
  }

  try {
    const position = await new Promise<GeolocationPosition>((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: false,
        timeout: timeoutMs,
        maximumAge: 60_000,
      });
    });

    return {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
    };
  } catch {
    return null;
  }
};
