import { RecognitionStatus, type AttendanceRecognitionReviewDto } from "../services/attendanceRecognitionService";

export type RecognitionReviewFilter =
  | "matched"
  | "unmatched"
  | "lowConfidence"
  | "rejected"
  | "manualOverride";

export const RECOGNITION_REVIEW_FILTERS: { id: RecognitionReviewFilter; label: string }[] = [
  { id: "matched", label: "Matched" },
  { id: "unmatched", label: "Unmatched" },
  { id: "lowConfidence", label: "Low confidence" },
  { id: "rejected", label: "Rejected" },
  { id: "manualOverride", label: "Manual override" },
];

function matchesFilter(row: AttendanceRecognitionReviewDto, filter: RecognitionReviewFilter): boolean {
  switch (filter) {
    case "matched":
      return row.isMatched;
    case "unmatched":
      return !row.isMatched;
    case "lowConfidence":
      return row.status === RecognitionStatus.LowConfidence;
    case "rejected":
      return row.status === RecognitionStatus.Rejected;
    case "manualOverride":
      return row.teacherOverride || row.status === RecognitionStatus.ManuallyAssigned;
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
  ]
    .filter(Boolean)
    .join(" ")
    .toLowerCase();

  return haystack.includes(q);
}

export function filterRecognitions(
  recognitions: AttendanceRecognitionReviewDto[],
  activeFilters: Set<RecognitionReviewFilter>,
  searchText: string
): AttendanceRecognitionReviewDto[] {
  return recognitions.filter((row) => {
    const filterOk =
      activeFilters.size === 0 || [...activeFilters].some((filter) => matchesFilter(row, filter));
    return filterOk && matchesSearch(row, searchText);
  });
}
