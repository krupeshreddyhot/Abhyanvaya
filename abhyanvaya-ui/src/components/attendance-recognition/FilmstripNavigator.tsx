import { Box, Chip, Stack, Typography } from "@mui/material";
import { mediaAssetUrl } from "../../utils/mediaAssetUrl";
import type { AttendanceSessionImage } from "../../types/sessionImage";
import { SESSION_IMAGE_STATUS } from "../../types/sessionImage";

export type FilmstripNavigatorProps = {
  images: AttendanceSessionImage[];
  activeImageSequence: number;
  sequencesWithFaces: Set<number>;
  onSelectSequence: (sequence: number) => void;
  onReorder?: (orderedIds: string[]) => void;
  /** AI22.7B 5.8 — personalized filmstrip thumbnail height. */
  thumbnailHeight?: number;
};

function statusBadge(status: number): { label: string; color: "default" | "success" | "warning" | "error" | "info" } {
  switch (status) {
    case SESSION_IMAGE_STATUS.Processed:
      return { label: "Ready", color: "success" };
    case SESSION_IMAGE_STATUS.Processing:
      return { label: "Processing", color: "warning" };
    case SESSION_IMAGE_STATUS.Failed:
      return { label: "Error", color: "error" };
    case SESSION_IMAGE_STATUS.Uploaded:
      return { label: "Needs Review", color: "info" };
    default:
      return { label: "Waiting", color: "default" };
  }
}

/** AI22.7A Phase 5.3 — Lightroom-style horizontal filmstrip. */
export function FilmstripNavigator({
  images,
  activeImageSequence,
  sequencesWithFaces,
  onSelectSequence,
  onReorder,
  thumbnailHeight = 64,
}: FilmstripNavigatorProps) {
  if (images.length === 0) {
    return null;
  }

  const ordered = [...images].sort((a, b) => a.imageSequence - b.imageSequence);
  const thumbH = Math.max(48, Math.min(120, thumbnailHeight - 32));

  return (
    <Stack spacing={0.75} aria-label="Classroom image filmstrip">
      <Typography variant="caption" color="text.secondary">
        Filmstrip · click / swipe to switch · drag to reorder
      </Typography>
      <Box
        sx={{
          display: "flex",
          gap: 1,
          overflowX: "auto",
          pb: 0.5,
          scrollBehavior: "smooth",
          WebkitOverflowScrolling: "touch",
          touchAction: "pan-x",
          "@media (prefers-reduced-motion: reduce)": { scrollBehavior: "auto" },
        }}
        role="listbox"
        aria-label="Classroom images"
      >
        {ordered.map((image, index) => {
          const active = image.imageSequence === activeImageSequence;
          const badge = statusBadge(Number(image.status));
          const url = mediaAssetUrl(image.imageUrl);
          return (
            <Box
              key={image.id}
              role="option"
              aria-selected={active}
              draggable={Boolean(onReorder)}
              onDragStart={(event) => {
                event.dataTransfer.setData("text/plain", String(index));
              }}
              onDragOver={(event) => event.preventDefault()}
              onDrop={(event) => {
                event.preventDefault();
                if (!onReorder) {
                  return;
                }
                const from = Number(event.dataTransfer.getData("text/plain"));
                if (Number.isNaN(from) || from === index) {
                  return;
                }
                const next = [...ordered];
                const [moved] = next.splice(from, 1);
                next.splice(index, 0, moved);
                onReorder(next.map((item) => item.id));
              }}
              onClick={() => onSelectSequence(image.imageSequence)}
              sx={{
                flex: "0 0 auto",
                width: 96,
                borderRadius: 1,
                border: 2,
                borderColor: active ? "primary.main" : "divider",
                overflow: "hidden",
                cursor: "pointer",
                bgcolor: "background.paper",
                transition: (theme) =>
                  theme.transitions.create(["border-color", "box-shadow", "transform"], {
                    duration: theme.transitions.duration.shorter,
                  }),
                boxShadow: active ? 2 : 0,
                transform: active ? "translateY(-2px)" : "none",
                "@media (prefers-reduced-motion: reduce)": { transition: "none", transform: "none" },
              }}
            >
              <Box
                component="img"
                src={url ?? undefined}
                alt={`Classroom image ${image.imageSequence}`}
                data-enterprise-media="true"
                sx={{ width: "100%", height: thumbH, objectFit: "cover", display: "block", bgcolor: "action.hover" }}
              />
              <Stack spacing={0.25} sx={{ p: 0.5 }}>
                <Typography variant="caption" sx={{ fontWeight: 700 }}>
                  #{image.imageSequence} {sequencesWithFaces.has(image.imageSequence) ? "●" : "○"}
                </Typography>
                <Chip size="small" label={badge.label} color={badge.color} sx={{ height: 18, fontSize: "0.65rem" }} />
              </Stack>
            </Box>
          );
        })}
      </Box>
    </Stack>
  );
}

export default FilmstripNavigator;
