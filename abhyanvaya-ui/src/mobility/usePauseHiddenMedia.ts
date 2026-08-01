import { useEffect, type RefObject } from "react";
import { detectDeviceCapability } from "./devicePerformance";

/**
 * AI22.7C Phase 1.7 — pause video when tab/document is hidden or element leaves viewport.
 * Battery-friendly; does not stop the MediaStream (restart stays cheap via play()).
 */
export function usePauseHiddenMedia(
  videoRef: RefObject<HTMLVideoElement | null>,
  enabled: boolean,
): void {
  useEffect(() => {
    if (!enabled) {
      return;
    }
    const profile = detectDeviceCapability();
    if (!profile.pauseHiddenVideo) {
      return;
    }

    const video = videoRef.current;
    if (!video) {
      return;
    }

    const resume = () => {
      if (!document.hidden && video.srcObject) {
        void video.play().catch(() => undefined);
      }
    };

    const pause = () => {
      if (!video.paused) {
        video.pause();
      }
    };

    const onVisibility = () => {
      if (document.hidden) {
        pause();
      } else {
        resume();
      }
    };

    document.addEventListener("visibilitychange", onVisibility);

    let observer: IntersectionObserver | null = null;
    if (typeof IntersectionObserver !== "undefined") {
      observer = new IntersectionObserver(
        (entries) => {
          const entry = entries[0];
          if (!entry) {
            return;
          }
          if (entry.isIntersecting && !document.hidden) {
            resume();
          } else {
            pause();
          }
        },
        { threshold: 0.05 },
      );
      observer.observe(video);
    }

    return () => {
      document.removeEventListener("visibilitychange", onVisibility);
      observer?.disconnect();
    };
  }, [enabled, videoRef]);
}
