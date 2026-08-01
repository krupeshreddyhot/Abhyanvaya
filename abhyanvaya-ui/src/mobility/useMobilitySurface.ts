import { useEffect, useMemo, useState } from "react";
import {
  isMobileCaptureWidth,
  isTabletReviewWidth,
  resolveMobilitySurface,
  type MobilitySurface,
} from "./breakpoints";

function readViewport() {
  if (typeof window === "undefined") {
    return { width: 1280, height: 800 };
  }
  return { width: window.innerWidth, height: window.innerHeight };
}

/** AI22.7C — viewport-driven mobility surface (layout only). */
export function useMobilitySurface() {
  const [viewport, setViewport] = useState(readViewport);

  useEffect(() => {
    const onResize = () => setViewport(readViewport());
    window.addEventListener("resize", onResize);
    window.addEventListener("orientationchange", onResize);
    return () => {
      window.removeEventListener("resize", onResize);
      window.removeEventListener("orientationchange", onResize);
    };
  }, []);

  const surface: MobilitySurface = useMemo(
    () => resolveMobilitySurface(viewport.width, viewport.height),
    [viewport.height, viewport.width],
  );

  return {
    surface,
    width: viewport.width,
    height: viewport.height,
    isPhone: surface === "phone",
    isTablet: surface === "tablet",
    isDesktop: surface === "desktop",
    isMobileCapture: isMobileCaptureWidth(viewport.width),
    isTabletReview: isTabletReviewWidth(viewport.width) || surface === "tablet",
    isLandscape: viewport.width >= viewport.height,
  };
}
