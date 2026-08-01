import type { AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";
import type { RecognitionStatisticsDto } from "../services/attendanceRecognitionService";
import { getEnterpriseConfidence } from "./enterpriseConfidence";
import { countBySmartCategory } from "./smartReviewQueue";
import { RecognitionStatus } from "../services/attendanceRecognitionService";

/** AI22.7A Phase 5.6 / 5.8 — client-side review analytics (existing DTOs only). */

export type ReviewAnalyticsSnapshot = {
  images: number;
  faces: number;
  students: number;
  approved: number;
  rejected: number;
  unknown: number;
  duplicates: number;
  pending: number;
  progressPercent: number;
  averageConfidence: number | null;
  lowestConfidence: number | null;
  recognitionTimeLabel: string;
  reviewTimeLabel: string;
  estimatedRemainingLabel: string;
  confidenceBuckets: { id: string; label: string; count: number }[];
  statusBuckets: { label: string; count: number }[];
};

export type SessionProductivitySnapshot = {
  elapsedLabel: string;
  studentsReviewed: number;
  facesReviewed: number;
  reviewsPerMinute: number;
  averageDecisionLabel: string;
  manualCorrections: number;
  approvalPercent: number;
  estimatedCompletionLabel: string;
  sessionScore: number;
};

/** Alias for toolbar strip (Phase 5.8). */
export type SessionProductivityMetrics = SessionProductivitySnapshot;

function formatMs(ms: number): string {
  const totalSec = Math.max(0, Math.floor(ms / 1000));
  const m = Math.floor(totalSec / 60);
  const s = totalSec % 60;
  return `${m}:${s.toString().padStart(2, "0")}`;
}

export function buildReviewAnalytics(input: {
  imageCount: number;
  recognitions: AttendanceRecognitionReviewDto[];
  statistics: RecognitionStatisticsDto | null;
  elapsedMs: number;
  averageDecisionMs: number;
  pendingCount: number;
}): ReviewAnalyticsSnapshot {
  const { recognitions, statistics, imageCount, elapsedMs, averageDecisionMs, pendingCount } = input;
  const cats = countBySmartCategory(recognitions);
  const confidences = recognitions
    .map((r) => r.confidence)
    .filter((c): c is number => c != null && !Number.isNaN(c));
  const averageConfidence =
    statistics?.averageConfidence ??
    (confidences.length
      ? confidences.reduce((a, b) => a + b, 0) / confidences.length
      : null);
  const lowestConfidence = confidences.length ? Math.min(...confidences) : null;

  const studentIds = new Set(
    recognitions
      .map((r) => r.studentId ?? r.manualOverrideStudentId ?? r.suggestedStudentId)
      .filter((id): id is number => id != null),
  );

  const buckets = [
    { id: "excellent", label: "Excellent", count: 0 },
    { id: "high", label: "High", count: 0 },
    { id: "medium", label: "Medium", count: 0 },
    { id: "low", label: "Low", count: 0 },
    { id: "unknown", label: "Unknown", count: 0 },
  ];
  for (const row of recognitions) {
    const id = getEnterpriseConfidence(row.confidence).filterId;
    if (id === "excellent") buckets[0].count += 1;
    else if (id === "high") buckets[1].count += 1;
    else if (id === "medium") buckets[2].count += 1;
    else if (id === "low") buckets[3].count += 1;
    else buckets[4].count += 1;
  }

  const estMs = pendingCount * (averageDecisionMs > 0 ? averageDecisionMs : 12_000);
  const approvedCount = cats.approved || statistics?.approved || 0;
  const rejectedCount = cats.rejected || statistics?.rejected || 0;
  const faces = recognitions.length || statistics?.detectedFaces || 0;
  const reviewedApprox = approvedCount + rejectedCount;
  const progressPercent = faces > 0 ? (reviewedApprox / faces) * 100 : 0;

  return {
    images: imageCount,
    faces,
    students: studentIds.size,
    approved: approvedCount,
    rejected: rejectedCount,
    unknown: cats.unknown,
    duplicates: cats.duplicate,
    pending: pendingCount,
    progressPercent,
    averageConfidence,
    lowestConfidence,
    recognitionTimeLabel: "—",
    reviewTimeLabel: formatMs(elapsedMs),
    estimatedRemainingLabel: formatMs(estMs),
    confidenceBuckets: buckets,
    statusBuckets: [
      { label: "Needs Review", count: cats.needsReview },
      { label: "Low Conf", count: cats.lowConfidence },
      { label: "Unknown", count: cats.unknown },
      { label: "Duplicate", count: cats.duplicate },
      { label: "Rejected", count: cats.rejected },
      { label: "Approved", count: cats.approved },
    ],
  };
}

export function buildSessionProductivity(input: {
  elapsedMs: number;
  recognitions: AttendanceRecognitionReviewDto[];
  decisionTimesMs: number[];
  pendingCount: number;
}): SessionProductivitySnapshot {
  const { elapsedMs, recognitions, decisionTimesMs, pendingCount } = input;
  const reviewed = recognitions.filter((r) => r.verifiedByTeacher);
  const approved = reviewed.filter((r) => r.status !== RecognitionStatus.Rejected);
  const manual = recognitions.filter((r) => r.teacherOverride || r.status === RecognitionStatus.ManuallyAssigned);
  const minutes = elapsedMs / 60_000;
  const reviewsPerMinute = minutes > 0 ? reviewed.length / minutes : 0;

  let avgDecision = 0;
  if (decisionTimesMs.length >= 2) {
    let total = 0;
    for (let i = 1; i < decisionTimesMs.length; i += 1) {
      total += decisionTimesMs[i] - decisionTimesMs[i - 1];
    }
    avgDecision = total / (decisionTimesMs.length - 1);
  }

  const approvalPercent =
    reviewed.length > 0 ? Math.round((approved.length / reviewed.length) * 100) : 0;
  const estMs = pendingCount * (avgDecision > 0 ? avgDecision : 12_000);

  // Session score: higher when more reviewed with fewer pending and decent approval rate.
  const progress = recognitions.length
    ? reviewed.length / recognitions.length
    : 0;
  const sessionScore = Math.round(
    Math.min(100, progress * 70 + (approvalPercent / 100) * 20 + Math.min(10, reviewsPerMinute * 2)),
  );

  return {
    elapsedLabel: formatMs(elapsedMs),
    studentsReviewed: new Set(reviewed.map((r) => r.studentId).filter(Boolean)).size,
    facesReviewed: reviewed.length,
    reviewsPerMinute: Math.round(reviewsPerMinute * 10) / 10,
    averageDecisionLabel: avgDecision > 0 ? formatMs(avgDecision) : "—",
    manualCorrections: manual.length,
    approvalPercent,
    estimatedCompletionLabel: formatMs(estMs),
    sessionScore,
  };
}
