import { RecognitionStatus, type AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";
import { getEnterpriseConfidence } from "./enterpriseConfidence";
import { isPendingReview } from "./recognitionStatus";

/** AI22.7A Phase 5.4 — smart review queue categories (UI only). */

export type SmartQueueCategory =
  | "needsReview"
  | "unknown"
  | "duplicate"
  | "lowConfidence"
  | "rejected"
  | "approved";

export const SMART_QUEUE_CATEGORIES: { id: SmartQueueCategory; label: string }[] = [
  { id: "needsReview", label: "Needs Review" },
  { id: "unknown", label: "Unknown" },
  { id: "duplicate", label: "Duplicate" },
  { id: "lowConfidence", label: "Low Confidence" },
  { id: "rejected", label: "Rejected" },
  { id: "approved", label: "Approved" },
];

export function categorizeRecognition(row: AttendanceRecognitionReviewDto): SmartQueueCategory {
  // Compare numerically — keeps compatibility if status unions lag enum updates.
  if (Number(row.status) === RecognitionStatus.Rejected) {
    return "rejected";
  }
  if (row.verifiedByTeacher && Number(row.status) !== RecognitionStatus.Rejected) {
    return "approved";
  }
  if (row.status === RecognitionStatus.Duplicate) {
    return "duplicate";
  }
  if (
    row.status === RecognitionStatus.Unknown ||
    row.status === RecognitionStatus.Ignored ||
    !row.isMatched
  ) {
    return "unknown";
  }
  const band = getEnterpriseConfidence(row.confidence).band;
  if (band === "low" || band === "unknown" || row.status === RecognitionStatus.LowConfidence) {
    return "lowConfidence";
  }
  if (isPendingReview(row.status, row.verifiedByTeacher)) {
    return "needsReview";
  }
  return "needsReview";
}

export function applySmartQueue(
  recognitions: AttendanceRecognitionReviewDto[],
  options: {
    category: SmartQueueCategory | "all";
    onlyPending: boolean;
    collapseApproved: boolean;
  },
): AttendanceRecognitionReviewDto[] {
  let rows = recognitions;

  if (options.onlyPending) {
    rows = rows.filter((row) => isPendingReview(row.status, row.verifiedByTeacher));
  }

  if (options.category !== "all") {
    rows = rows.filter((row) => categorizeRecognition(row) === options.category);
  }

  if (options.collapseApproved) {
    rows = rows.filter((row) => categorizeRecognition(row) !== "approved");
  }

  const order: SmartQueueCategory[] = [
    "needsReview",
    "lowConfidence",
    "unknown",
    "duplicate",
    "rejected",
    "approved",
  ];

  return [...rows].sort((a, b) => {
    const ca = order.indexOf(categorizeRecognition(a));
    const cb = order.indexOf(categorizeRecognition(b));
    if (ca !== cb) {
      return ca - cb;
    }
    const confA = a.confidence ?? -1;
    const confB = b.confidence ?? -1;
    return confA - confB;
  });
}

export function estimateReviewMinutesRemaining(pendingCount: number, averageDecisionMs: number): number {
  const avg = averageDecisionMs > 0 ? averageDecisionMs : 12_000;
  return Math.max(0, Math.round((pendingCount * avg) / 60_000));
}

export function countBySmartCategory(
  recognitions: AttendanceRecognitionReviewDto[],
): Record<SmartQueueCategory, number> {
  const counts: Record<SmartQueueCategory, number> = {
    needsReview: 0,
    unknown: 0,
    duplicate: 0,
    lowConfidence: 0,
    rejected: 0,
    approved: 0,
  };
  for (const row of recognitions) {
    counts[categorizeRecognition(row)] += 1;
  }
  return counts;
}
