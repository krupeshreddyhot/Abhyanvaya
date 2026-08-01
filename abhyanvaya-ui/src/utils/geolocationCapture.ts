export type CaptureGeoLocation = {
  latitude: number;
  longitude: number;
};

/**
 * Best-effort geolocation for capture metadata. Never blocks capture on denial/timeout.
 */
export const tryGetCaptureLocation = async (
  timeoutMs = 1500,
): Promise<CaptureGeoLocation | null> => {
  if (typeof navigator === "undefined" || !navigator.geolocation) {
    return null;
  }

  try {
    const position = await new Promise<GeolocationPosition>((resolve, reject) => {
      let settled = false;
      const finish = (fn: () => void) => {
        if (settled) {
          return;
        }
        settled = true;
        fn();
      };

      // Hard ceiling: some browsers do not honor the Geolocation `timeout` option while a
      // permission prompt is open, which made Upload Image appear to do nothing.
      const timer = window.setTimeout(() => {
        finish(() => reject(new Error("Geolocation timed out.")));
      }, timeoutMs);

      navigator.geolocation.getCurrentPosition(
        (value) => {
          window.clearTimeout(timer);
          finish(() => resolve(value));
        },
        (error) => {
          window.clearTimeout(timer);
          finish(() => reject(error));
        },
        {
          enableHighAccuracy: false,
          timeout: timeoutMs,
          maximumAge: 60_000,
        },
      );
    });

    return {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
    };
  } catch {
    return null;
  }
};
