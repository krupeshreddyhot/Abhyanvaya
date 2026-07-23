/**
 * AI22.7A-R1 — enterprise image quality labels from existing blur analysis.
 * Does not change backend algorithms; presentation only.
 */

export type ImageQualityLevel =
  | "Excellent"
  | "Good"
  | "Acceptable"
  | "RetakeRecommended"
  | "Poor"
  | "Unknown";

export type ImageQualityIndicator = {
  level: ImageQualityLevel;
  label: string;
  stars: string;
  shortLabel: string;
  /** 1–5 for sorting; 0 = unknown */
  rank: number;
};

/** Thresholds derived from CLASSROOM_PHOTO_BLUR_WARN_THRESHOLD (80) — UI only. */
export const IMAGE_QUALITY_THRESHOLDS = {
  excellent: 200,
  good: 120,
  acceptable: 80,
  retake: 40,
} as const;

export const getImageQualityIndicator = (
  blurScore: number | null | undefined,
): ImageQualityIndicator => {
  if (blurScore == null || Number.isNaN(blurScore)) {
    return {
      level: "Unknown",
      label: "Quality pending",
      stars: "☆☆☆☆☆",
      shortLabel: "Pending",
      rank: 0,
    };
  }

  if (blurScore >= IMAGE_QUALITY_THRESHOLDS.excellent) {
    return {
      level: "Excellent",
      label: "Excellent",
      stars: "★★★★★",
      shortLabel: "Excellent",
      rank: 5,
    };
  }

  if (blurScore >= IMAGE_QUALITY_THRESHOLDS.good) {
    return {
      level: "Good",
      label: "Good",
      stars: "★★★★☆",
      shortLabel: "Good",
      rank: 4,
    };
  }

  if (blurScore >= IMAGE_QUALITY_THRESHOLDS.acceptable) {
    return {
      level: "Acceptable",
      label: "Acceptable",
      stars: "★★★☆☆",
      shortLabel: "Acceptable",
      rank: 3,
    };
  }

  if (blurScore >= IMAGE_QUALITY_THRESHOLDS.retake) {
    return {
      level: "RetakeRecommended",
      label: "Retake Recommended",
      stars: "★★☆☆☆",
      shortLabel: "Retake",
      rank: 2,
    };
  }

  return {
    level: "Poor",
    label: "Poor",
    stars: "★☆☆☆☆",
    shortLabel: "Poor",
    rank: 1,
  };
};

/** Soft pre-AI face estimate for display only (not model output). */
export const estimateFacesFromResolution = (
  width: number | null | undefined,
  height: number | null | undefined,
): string => {
  if (!width || !height || width <= 0 || height <= 0) {
    return "Pending";
  }

  const megapixels = (width * height) / 1_000_000;
  if (megapixels >= 4) {
    return "~20–40";
  }
  if (megapixels >= 2) {
    return "~10–25";
  }
  if (megapixels >= 1) {
    return "~5–15";
  }
  return "~1–8";
};

export const formatCaptureTime = (value: Date | string | null | undefined): string => {
  if (!value) {
    return "—";
  }

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }

  return date.toLocaleString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    day: "2-digit",
    month: "short",
  });
};
