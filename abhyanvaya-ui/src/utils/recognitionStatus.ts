import { RecognitionStatus, type RecognitionStatusValue } from "../services/attendanceRecognitionService";

const STATUS_LABELS: Record<RecognitionStatusValue, string> = {
  [RecognitionStatus.Unknown]: "Unknown",
  [RecognitionStatus.Recognized]: "Recognized",
  [RecognitionStatus.LowConfidence]: "Low confidence",
  [RecognitionStatus.Duplicate]: "Duplicate",
  [RecognitionStatus.Ignored]: "Ignored",
  [RecognitionStatus.Rejected]: "Rejected",
  [RecognitionStatus.ManuallyAssigned]: "Manually assigned",
};

const STATUS_COLORS: Record<RecognitionStatusValue, "default" | "success" | "warning" | "error" | "info"> = {
  [RecognitionStatus.Unknown]: "default",
  [RecognitionStatus.Recognized]: "success",
  [RecognitionStatus.LowConfidence]: "warning",
  [RecognitionStatus.Duplicate]: "info",
  [RecognitionStatus.Ignored]: "default",
  [RecognitionStatus.Rejected]: "error",
  [RecognitionStatus.ManuallyAssigned]: "info",
};

export function recognitionStatusLabel(status: RecognitionStatusValue): string {
  return STATUS_LABELS[status] ?? "Unknown";
}

export function recognitionStatusColor(
  status: RecognitionStatusValue
): "default" | "success" | "warning" | "error" | "info" {
  return STATUS_COLORS[status] ?? "default";
}

export function isPendingReview(status: RecognitionStatusValue, verifiedByTeacher: boolean): boolean {
  return !verifiedByTeacher
    || status === RecognitionStatus.Unknown
    || status === RecognitionStatus.LowConfidence;
}
