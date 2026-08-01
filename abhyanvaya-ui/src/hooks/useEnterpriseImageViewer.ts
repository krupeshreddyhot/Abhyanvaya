import { useCallback, useEffect, useRef, useState } from "react";

export type FitMode = "width" | "height" | "screen" | "100" | "200" | "400" | "custom";

export type EnterpriseImageViewerState = {
  scale: number;
  offsetX: number;
  offsetY: number;
  fitMode: FitMode;
};

const MIN_SCALE = 0.25;
const MAX_SCALE = 8;
const ZOOM_STEP = 1.15;

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * AI22.7A Phase 4.1 — GPU-friendly zoom/pan state for the recognition image canvas.
 * Overlays share the same transform so bounding boxes stay aligned without re-layout.
 */
export function useEnterpriseImageViewer() {
  const [scale, setScale] = useState(1);
  const [offsetX, setOffsetX] = useState(0);
  const [offsetY, setOffsetY] = useState(0);
  const [fitMode, setFitMode] = useState<FitMode>("screen");
  const [panning, setPanning] = useState(false);
  const panOrigin = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);
  const pinchOrigin = useRef<{
    distance: number;
    scale: number;
    midX: number;
    midY: number;
    ox: number;
    oy: number;
  } | null>(null);
  /** Alt (or Option) held — Phase 5.7 reserves Space for next-face navigation. */
  const altPanDown = useRef(false);
  /** AI22.7C Phase 1.3 — double-tap zoom tracking. */
  const lastTapRef = useRef<number | null>(null);

  const zoomBy = useCallback((factor: number, originX = 0, originY = 0) => {
    setScale((prev) => {
      const next = clamp(prev * factor, MIN_SCALE, MAX_SCALE);
      const ratio = next / prev;
      setOffsetX((ox) => originX - (originX - ox) * ratio);
      setOffsetY((oy) => originY - (originY - oy) * ratio);
      return next;
    });
    setFitMode("custom");
  }, []);

  const zoomIn = useCallback(() => zoomBy(ZOOM_STEP), [zoomBy]);
  const zoomOut = useCallback(() => zoomBy(1 / ZOOM_STEP), [zoomBy]);

  const setZoomPercent = useCallback((percent: number) => {
    setScale(clamp(percent / 100, MIN_SCALE, MAX_SCALE));
    setOffsetX(0);
    setOffsetY(0);
    setFitMode(
      percent === 100 ? "100" : percent === 200 ? "200" : percent === 400 ? "400" : "custom",
    );
  }, []);

  const resetView = useCallback(() => {
    setScale(1);
    setOffsetX(0);
    setOffsetY(0);
    setFitMode("screen");
  }, []);

  const fit = useCallback((mode: FitMode) => {
    setOffsetX(0);
    setOffsetY(0);
    setFitMode(mode);
    if (mode === "100") setScale(1);
    else if (mode === "200") setScale(2);
    else if (mode === "400") setScale(4);
    else setScale(1);
  }, []);

  const onWheel = useCallback(
    (event: React.WheelEvent) => {
      event.preventDefault();
      const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
      const originX = event.clientX - rect.left - rect.width / 2;
      const originY = event.clientY - rect.top - rect.height / 2;
      zoomBy(event.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP, originX, originY);
    },
    [zoomBy],
  );

  const onPointerDown = useCallback(
    (event: React.PointerEvent) => {
      const isMiddle = event.button === 1;
      const isAltDrag = event.button === 0 && (event.altKey || altPanDown.current);
      if (!isMiddle && !isAltDrag) {
        return;
      }
      event.preventDefault();
      (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
      setPanning(true);
      panOrigin.current = {
        x: event.clientX,
        y: event.clientY,
        ox: offsetX,
        oy: offsetY,
      };
    },
    [offsetX, offsetY],
  );

  const onPointerMove = useCallback((event: React.PointerEvent) => {
    if (!panOrigin.current) {
      return;
    }
    setOffsetX(panOrigin.current.ox + (event.clientX - panOrigin.current.x));
    setOffsetY(panOrigin.current.oy + (event.clientY - panOrigin.current.y));
    setFitMode("custom");
  }, []);

  const onPointerUp = useCallback((event: React.PointerEvent) => {
    try {
      (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);
    } catch {
      // ignore
    }
    panOrigin.current = null;
    setPanning(false);
  }, []);

  const onTouchStart = useCallback(
    (event: React.TouchEvent) => {
      if (event.touches.length === 2) {
        const [a, b] = [event.touches[0], event.touches[1]];
        const distance = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
        pinchOrigin.current = {
          distance,
          scale,
          midX: (a.clientX + b.clientX) / 2,
          midY: (a.clientY + b.clientY) / 2,
          ox: offsetX,
          oy: offsetY,
        };
        return;
      }

      // AI22.7C — double-tap toggles ~2x zoom (touch only; mouse unchanged).
      if (event.touches.length === 1) {
        const now = Date.now();
        if (lastTapRef.current != null && now - lastTapRef.current < 280) {
          event.preventDefault();
          setScale((prev) => {
            const next = prev > 1.5 ? 1 : 2;
            setOffsetX(0);
            setOffsetY(0);
            setFitMode(next === 1 ? "screen" : "custom");
            return next;
          });
          lastTapRef.current = null;
        } else {
          lastTapRef.current = now;
        }
      }
    },
    [offsetX, offsetY, scale],
  );

  const onTouchMove = useCallback((event: React.TouchEvent) => {
    if (event.touches.length === 2 && pinchOrigin.current) {
      event.preventDefault();
      const [a, b] = [event.touches[0], event.touches[1]];
      const distance = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
      const next = clamp(
        pinchOrigin.current.scale * (distance / pinchOrigin.current.distance),
        MIN_SCALE,
        MAX_SCALE,
      );
      setScale(next);
      // Two-finger pan: track midpoint delta (AI22.7C Phase 1.3).
      const midX = (a.clientX + b.clientX) / 2;
      const midY = (a.clientY + b.clientY) / 2;
      setOffsetX(pinchOrigin.current.ox + (midX - pinchOrigin.current.midX));
      setOffsetY(pinchOrigin.current.oy + (midY - pinchOrigin.current.midY));
      setFitMode("custom");
    }
  }, []);

  const onTouchEnd = useCallback(() => {
    pinchOrigin.current = null;
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Alt") {
        altPanDown.current = true;
      }
    };
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key === "Alt") {
        altPanDown.current = false;
      }
    };
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
    };
  }, []);

  const panByKeys = useCallback((dx: number, dy: number) => {
    setOffsetX((x) => x + dx);
    setOffsetY((y) => y + dy);
    setFitMode("custom");
  }, []);

  /** AI22.7A Phase 5.2 — minimap drag updates the shared viewer offset. */
  const setOffset = useCallback((nextX: number, nextY: number) => {
    setOffsetX(nextX);
    setOffsetY(nextY);
    setFitMode("custom");
  }, []);

  const transformStyle = {
    transform: `translate3d(${offsetX}px, ${offsetY}px, 0) scale(${scale})`,
    transformOrigin: "center center",
    willChange: "transform" as const,
  };

  return {
    scale,
    offsetX,
    offsetY,
    fitMode,
    panning,
    zoomIn,
    zoomOut,
    setZoomPercent,
    resetView,
    fit,
    setOffset,
    onWheel,
    onPointerDown,
    onPointerMove,
    onPointerUp,
    onTouchStart,
    onTouchMove,
    onTouchEnd,
    panByKeys,
    transformStyle,
    percent: Math.round(scale * 100),
  };
}

export default useEnterpriseImageViewer;
