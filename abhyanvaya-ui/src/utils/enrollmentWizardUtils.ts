import type { EnrollmentFilters } from "../types/enrollment";

export const WIZARD_STEPS = ["Academic Year", "Enrollment Scope", "Preview", "Confirm"] as const;

export type WizardScopeSelection = {
  courseId: number | "";
  groupId: number | "";
  semesterId: number | "";
  batch: number | "";
};

export const buildScopeFilters = (
  collegeId: number,
  academicYear: number,
  scope: WizardScopeSelection,
): EnrollmentFilters => ({
  collegeId,
  academicYear,
  courseId: scope.courseId ? Number(scope.courseId) : undefined,
  groupId: scope.groupId ? Number(scope.groupId) : undefined,
  batch: scope.batch ? Number(scope.batch) : undefined,
  subjectId: scope.semesterId ? Number(scope.semesterId) : undefined,
});

export const describeEnrollmentScope = (
  scope: WizardScopeSelection,
  labels: { course?: string; group?: string; semester?: string },
): string => {
  const parts: string[] = [];
  if (scope.courseId && labels.course) parts.push(labels.course);
  else if (!scope.courseId) parts.push("All courses");
  if (scope.groupId && labels.group) parts.push(labels.group);
  else if (!scope.groupId) parts.push("All groups");
  if (scope.semesterId && labels.semester) parts.push(labels.semester);
  else if (!scope.semesterId) parts.push("All semesters");
  if (scope.batch) parts.push(`Section ${scope.batch}`);
  return parts.join(" · ");
};

/** Parse ISO-8601 duration (e.g. PT2M30S) into seconds for estimates. */
export const parseIsoDurationSeconds = (isoDuration: string | null | undefined): number | null => {
  if (!isoDuration) return null;
  const match = /P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?)?/.exec(isoDuration);
  if (!match) {
    const legacy = /(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})/.exec(isoDuration);
    if (!legacy) return null;
    const days = Number(legacy[1] ?? 0);
    return days * 86400 + Number(legacy[2]) * 3600 + Number(legacy[3]) * 60 + Number(legacy[4]);
  }
  const days = Number(match[1] ?? 0);
  const hours = Number(match[2] ?? 0);
  const minutes = Number(match[3] ?? 0);
  const seconds = Number(match[4] ?? 0);
  return days * 86400 + hours * 3600 + minutes * 60 + seconds;
};

export const estimateProcessingSeconds = (
  eligibleCount: number,
  averageDurationIso: string | null | undefined,
  downloadThreads: number,
): number | null => {
  const perItem = parseIsoDurationSeconds(averageDurationIso);
  if (perItem == null || eligibleCount <= 0) return null;
  const parallelism = Math.max(1, downloadThreads);
  return Math.ceil((eligibleCount * perItem) / parallelism);
};

export const formatEstimatedDuration = (totalSeconds: number | null): string => {
  if (totalSeconds == null) return "—";
  if (totalSeconds < 60) return "< 1 minute";
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  if (hours > 0) return `~${hours}h ${minutes}m`;
  return `~${minutes} minute${minutes === 1 ? "" : "s"}`;
};

export const deriveSimilarityMetric = (recognitionEngine: string | undefined): string => {
  if (!recognitionEngine) return "—";
  if (recognitionEngine.toLowerCase().includes("insight")) return "Cosine Similarity";
  return "Configured";
};
