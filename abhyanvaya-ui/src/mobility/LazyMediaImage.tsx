import { Box, type BoxProps } from "@mui/material";
import { adaptiveImageQuality, detectDeviceCapability } from "./devicePerformance";

export type LazyMediaImageProps = Omit<BoxProps<"img">, "component"> & {
  alt: string;
  src?: string;
  /** When true, apply lite CSS downscale hint on constrained devices. */
  adaptiveQuality?: boolean;
};

/**
 * AI22.7C Phase 1.7 — lazy / async media with optional adaptive quality hint.
 * Presentation only; does not alter upload or recognition payloads.
 */
export function LazyMediaImage({
  alt,
  src,
  adaptiveQuality = true,
  sx,
  ...rest
}: LazyMediaImageProps) {
  const profile = detectDeviceCapability();
  const quality = adaptiveQuality ? adaptiveImageQuality(profile) : "high";
  const lite = quality !== "high";

  return (
    <Box
      component="img"
      src={src}
      alt={alt}
      loading="lazy"
      decoding="async"
      data-enterprise-media="true"
      data-adaptive-quality={quality}
      sx={{
        display: "block",
        ...(lite
          ? {
              imageRendering: "auto",
              filter: quality === "low" ? "contrast(0.98)" : "none",
              // Soft hint only — never mutates source bytes.
              maxWidth: "100%",
            }
          : null),
        ...sx,
      }}
      {...rest}
    />
  );
}

export default LazyMediaImage;
