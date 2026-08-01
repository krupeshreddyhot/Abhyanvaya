import { createContext, useContext, useMemo, useState, type ReactNode } from "react";

type ReviewFullscreenContextValue = {
  fullscreen: boolean;
  setFullscreen: (value: boolean) => void;
  toggleFullscreen: () => void;
};

const ReviewFullscreenContext = createContext<ReviewFullscreenContextValue | null>(null);

/** AI22.7A Phase 5.1 — workspace fullscreen chrome control (layout + review page). */
export function ReviewFullscreenProvider({ children }: { children: ReactNode }) {
  const [fullscreen, setFullscreen] = useState(false);
  const value = useMemo(
    () => ({
      fullscreen,
      setFullscreen,
      toggleFullscreen: () => setFullscreen((current) => !current),
    }),
    [fullscreen],
  );
  return <ReviewFullscreenContext.Provider value={value}>{children}</ReviewFullscreenContext.Provider>;
}

export function useReviewFullscreen(): ReviewFullscreenContextValue {
  const ctx = useContext(ReviewFullscreenContext);
  if (!ctx) {
    return {
      fullscreen: false,
      setFullscreen: () => undefined,
      toggleFullscreen: () => undefined,
    };
  }
  return ctx;
}
