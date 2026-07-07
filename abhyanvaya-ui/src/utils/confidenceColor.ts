/** Shared confidence bands — no magic numbers in components. */
export const CONFIDENCE_BANDS = {
  excellent: { min: 95, max: 100, color: "#2e7d32", label: "Excellent" },
  high: { min: 85, max: 94.99, color: "#1976d2", label: "High" },
  moderate: { min: 70, max: 84.99, color: "#ed6c02", label: "Moderate" },
  low: { min: 0, max: 69.99, color: "#d32f2f", label: "Low" },
  unknown: { min: -1, max: -1, color: "#757575", label: "Unknown" },
} as const;

export type ConfidenceBandKey = keyof typeof CONFIDENCE_BANDS;

export function getConfidenceBand(score: number | null | undefined): ConfidenceBandKey {
  if (score == null || Number.isNaN(score)) {
    return "unknown";
  }

  if (score >= CONFIDENCE_BANDS.excellent.min) {
    return "excellent";
  }

  if (score >= CONFIDENCE_BANDS.high.min) {
    return "high";
  }

  if (score >= CONFIDENCE_BANDS.moderate.min) {
    return "moderate";
  }

  return "low";
}

export function confidenceColor(score: number | null | undefined): string {
  return CONFIDENCE_BANDS[getConfidenceBand(score)].color;
}

export function confidenceBarValue(score: number | null | undefined): number {
  if (score == null || Number.isNaN(score)) {
    return 0;
  }

  return Math.max(0, Math.min(100, score));
}

export function formatConfidence(score: number | null | undefined): string {
  if (score == null || Number.isNaN(score)) {
    return "—";
  }

  return `${score.toFixed(1)}%`;
}
