import { RecognitionStatus, type AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";
import { getEnterpriseConfidence } from "./enterpriseConfidence";

export type RecognitionReviewFilter =
  | "approved"
  | "rejected"
  | "unknown"
  | "manual"
  | "duplicate"
  | "lowConfidence"
  | "matched"
  | "unmatched"
  | "manualOverride"
  | "confidenceExcellent"
  | "confidenceHigh"
  | "confidenceMedium"
  | "confidenceLow"
  | "confidenceUnknown";

export const RECOGNITION_REVIEW_FILTERS: { id: RecognitionReviewFilter; label: string; group?: string }[] = [
  { id: "approved", label: "Approved", group: "status" },
  { id: "rejected", label: "Rejected", group: "status" },
  { id: "unknown", label: "Unknown", group: "status" },
  { id: "manual", label: "Manual", group: "status" },
  { id: "duplicate", label: "Duplicate", group: "status" },
  { id: "lowConfidence", label: "Low confidence", group: "status" },
  { id: "matched", label: "Matched", group: "legacy" },
  { id: "unmatched", label: "Unmatched", group: "legacy" },
  { id: "manualOverride", label: "Manual override", group: "legacy" },
  { id: "confidenceExcellent", label: "Excellent", group: "confidence" },
  { id: "confidenceHigh", label: "High", group: "confidence" },
  { id: "confidenceMedium", label: "Medium", group: "confidence" },
  { id: "confidenceLow", label: "Low", group: "confidence" },
  { id: "confidenceUnknown", label: "Unknown conf.", group: "confidence" },
];

function matchesFilter(row: AttendanceRecognitionReviewDto, filter: RecognitionReviewFilter): boolean {
  const confidence = getEnterpriseConfidence(row.confidence);

  switch (filter) {
    case "approved":
      return row.verifiedByTeacher && row.status !== RecognitionStatus.Rejected;
    case "rejected":
      return row.status === RecognitionStatus.Rejected;
    case "unknown":
      return row.status === RecognitionStatus.Unknown || row.status === RecognitionStatus.Ignored;
    case "manual":
      return row.teacherOverride || row.status === RecognitionStatus.ManuallyAssigned;
    case "duplicate":
      return row.status === RecognitionStatus.Duplicate;
    case "lowConfidence":
      return row.status === RecognitionStatus.LowConfidence || confidence.band === "low";
    case "matched":
      return row.isMatched;
    case "unmatched":
      return !row.isMatched;
    case "manualOverride":
      return row.teacherOverride || row.status === RecognitionStatus.ManuallyAssigned;
    case "confidenceExcellent":
      return confidence.filterId === "excellent";
    case "confidenceHigh":
      return confidence.filterId === "high";
    case "confidenceMedium":
      return confidence.filterId === "medium";
    case "confidenceLow":
      return confidence.filterId === "low";
    case "confidenceUnknown":
      return confidence.filterId === "unknown";
    default:
      return true;
  }
}

function matchesSearch(row: AttendanceRecognitionReviewDto, search: string): boolean {
  const q = search.trim().toLowerCase();
  if (!q) {
    return true;
  }

  const haystack = [
    row.studentNumber,
    row.studentName,
    row.suggestedStudentNumber,
    row.suggestedStudentName,
    row.manualOverrideStudentNumber,
    row.manualOverrideStudentName,
    row.studentId != null ? String(row.studentId) : null,
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  return haystack.includes(q);
}

export function filterRecognitions(
  recognitions: AttendanceRecognitionReviewDto[],
  activeFilters: Set<RecognitionReviewFilter>,
  searchText: string,
  options?: { hideHighConfidence?: boolean },
): AttendanceRecognitionReviewDto[] {
  return recognitions.filter((row) => {
    if (options?.hideHighConfidence) {
      const band = getEnterpriseConfidence(row.confidence).band;
      if (band === "excellent" || band === "high") {
        return false;
      }
    }

    const filterOk =
      activeFilters.size === 0 || [...activeFilters].some((filter) => matchesFilter(row, filter));
    return filterOk && matchesSearch(row, searchText);
  });
}

/** Related face ids for the same student across all classroom images. */
export function getRelatedRecognitionIds(
  recognitions: AttendanceRecognitionReviewDto[],
  focused: AttendanceRecognitionReviewDto | null,
): Set<string> {
  if (!focused) {
    return new Set();
  }

  const studentKey =
    focused.studentId ??
    focused.manualOverrideStudentId ??
    focused.suggestedStudentId ??
    null;

  if (studentKey == null) {
    return new Set([focused.recognitionId]);
  }

  return new Set(
    recognitions
      .filter((row) => {
        const key =
          row.studentId ?? row.manualOverrideStudentId ?? row.suggestedStudentId ?? null;
        return key === studentKey;
      })
      .map((row) => row.recognitionId),
  );
}
