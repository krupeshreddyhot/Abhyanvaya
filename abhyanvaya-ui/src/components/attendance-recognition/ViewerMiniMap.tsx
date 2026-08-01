import { Box } from "@mui/material";
import { useCallback, useRef } from "react";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";

export type ViewerMiniMapProps = {
  imageUrl: string | null;
  scale: number;
  offsetX: number;
  offsetY: number;
  onOffsetChange: (offsetX: number, offsetY: number) => void;
  visible?: boolean;
};

/**
 * AI22.7A Phase 5.2 — minimap overview; dragging the viewport rectangle pans the main viewer.
 * Reuses the same offset/scale state (no duplicate transforms on the main canvas).
 */
export function ViewerMiniMap({
  imageUrl,
  scale,
  offsetX,
  offsetY,
  onOffsetChange,
  visible = true,
}: ViewerMiniMapProps) {
  const resolved = mediaAssetUrl(imageUrl);
  const dragRef = useRef<{ x: number; y: number; ox: number; oy: number } | null>(null);

  const onPointerDown = useCallback(
    (event: React.PointerEvent) => {
      event.stopPropagation();
      (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
      dragRef.current = {
        x: event.clientX,
        y: event.clientY,
        ox: offsetX,
        oy: offsetY,
      };
    },
    [offsetX, offsetY],
  );

  const onPointerMove = useCallback(
    (event: React.PointerEvent) => {
      if (!dragRef.current) {
        return;
      }
      event.stopPropagation();
      // Invert drag so minimap drag direction matches expected map UX.
      const dx = (event.clientX - dragRef.current.x) * (1 / Math.max(scale, 0.25)) * 2;
      const dy = (event.clientY - dragRef.current.y) * (1 / Math.max(scale, 0.25)) * 2;
      onOffsetChange(dragRef.current.ox - dx, dragRef.current.oy - dy);
    },
    [onOffsetChange, scale],
  );

  const onPointerUp = useCallback((event: React.PointerEvent) => {
    try {
      (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);
    } catch {
      // ignore
    }
    dragRef.current = null;
  }, []);

  if (!visible || !resolved) {
    return null;
  }

  const viewportSize = Math.max(18, Math.min(72, 48 / scale));
  const left = 50 - offsetX / 8 - viewportSize / 2;
  const top = 50 - offsetY / 8 - viewportSize / 2;

  return (
    <Box
      aria-label="Image mini map"
      role="application"
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={onPointerUp}
      sx={{
        position: "absolute",
        right: 8,
        bottom: 8,
        width: 132,
        height: 96,
        borderRadius: 1,
        border: "1px solid",
        borderColor: "divider",
        overflow: "hidden",
        bgcolor: "grey.900",
        cursor: "grab",
        touchAction: "none",
        zIndex: 2,
        boxShadow: 2,
        "&:active": { cursor: "grabbing" },
      }}
    >
      <Box
        component="img"
        src={resolved}
        alt=""
        draggable={false}
        sx={{ width: "100%", height: "100%", objectFit: "cover", opacity: 0.85, pointerEvents: "none" }}
      />
      <Box
        aria-hidden
        sx={{
          position: "absolute",
          left: `${Math.min(85, Math.max(2, left))}%`,
          top: `${Math.min(80, Math.max(2, top))}%`,
          width: `${viewportSize}%`,
          height: `${viewportSize}%`,
          border: "2px solid",
          borderColor: "primary.light",
          bgcolor: "primary.main",
          opacity: 0.35,
          pointerEvents: "none",
        }}
      />
    </Box>
  );
}

export default ViewerMiniMap;
