import {
  CONFIDENCE_BANDS,
  getConfidenceBand,
  type ConfidenceBandKey,
} from "./confidenceColor";

/** AI22.7A Phase 4.3 — enterprise confidence presentation (UI only). */

export type EnterpriseConfidenceView = {
  band: ConfidenceBandKey;
  percentLabel: string;
  stars: string;
  label: string;
  bboxColor: string;
  filterId: "excellent" | "high" | "medium" | "low" | "unknown";
};

const STARS: Record<ConfidenceBandKey, string> = {
  excellent: "★★★★★",
  high: "★★★★☆",
  moderate: "★★★☆☆",
  low: "★★☆☆☆",
  unknown: "★☆☆☆☆",
};

const ENTERPRISE_LABELS: Record<ConfidenceBandKey, string> = {
  excellent: "Excellent",
  high: "High",
  moderate: "Medium",
  low: "Needs Review",
  unknown: "Manual Review Required",
};

/** Bounding-box colors per Phase 4.3: Green / Yellow / Orange / Gray */
const BBOX_COLORS: Record<ConfidenceBandKey, string> = {
  excellent: "#2e7d32",
  high: "#2e7d32",
  moderate: "#f9a825",
  low: "#ef6c00",
  unknown: "#9e9e9e",
};

export function getEnterpriseConfidence(
  score: number | null | undefined,
): EnterpriseConfidenceView {
  const band = getConfidenceBand(score);
  const percentLabel =
    score == null || Number.isNaN(score) ? "—" : `${Math.round(score)}%`;

  return {
    band,
    percentLabel,
    stars: STARS[band],
    label: score != null && score < 50 && band === "low"
      ? "Manual Review Required"
      : ENTERPRISE_LABELS[band],
    bboxColor: BBOX_COLORS[band],
    filterId:
      band === "moderate"
        ? "medium"
        : band === "excellent" || band === "high" || band === "low" || band === "unknown"
          ? band
          : "unknown",
  };
}

export const CONFIDENCE_LEGEND = [
  { id: "excellent" as const, label: "Excellent", color: BBOX_COLORS.excellent, stars: STARS.excellent },
  { id: "high" as const, label: "High", color: BBOX_COLORS.high, stars: STARS.high },
  { id: "medium" as const, label: "Medium", color: BBOX_COLORS.moderate, stars: STARS.moderate },
  { id: "low" as const, label: "Needs Review", color: BBOX_COLORS.low, stars: STARS.low },
  { id: "unknown" as const, label: "Unknown", color: BBOX_COLORS.unknown, stars: STARS.unknown },
];

export { CONFIDENCE_BANDS, getConfidenceBand };
