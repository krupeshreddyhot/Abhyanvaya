import { getEnterpriseConfidence } from "./enterpriseConfidence";

/** AI22.7A Phase 5.5 — heat-map colors for face overlays (GPU fill only). */

export type HeatMapTone = "high" | "medium" | "review" | "unknown";

export function getHeatMapTone(confidence: number | null | undefined): HeatMapTone {
  const band = getEnterpriseConfidence(confidence).band;
  if (band === "excellent" || band === "high") {
    return "high";
  }
  if (band === "moderate") {
    return "medium";
  }
  if (band === "low") {
    return "review";
  }
  return "unknown";
}

export const HEAT_MAP_COLORS: Record<HeatMapTone, string> = {
  high: "#2e7d32",
  medium: "#f9a825",
  review: "#ef6c00",
  unknown: "#d32f2f",
};

export const HEAT_MAP_LEGEND = [
  { tone: "high" as const, label: "High", color: HEAT_MAP_COLORS.high },
  { tone: "medium" as const, label: "Medium", color: HEAT_MAP_COLORS.medium },
  { tone: "review" as const, label: "Review", color: HEAT_MAP_COLORS.review },
  { tone: "unknown" as const, label: "Unknown", color: HEAT_MAP_COLORS.unknown },
];

/** Alias used by heat-map controls UI. */
export const HEATMAP_BANDS = HEAT_MAP_LEGEND.map((band) => ({
  id: band.tone,
  label: band.label,
  color: band.color,
}));
