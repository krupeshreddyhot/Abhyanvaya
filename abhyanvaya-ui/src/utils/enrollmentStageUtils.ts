import { BatchProgressState, type BatchProgressDto } from "../types/enrollment";

export type EnrollmentStageKey = "download" | "validate" | "embed" | "upload" | "complete";

export type EnrollmentStageDefinition = {
  key: EnrollmentStageKey;
  label: string;
  state: (typeof BatchProgressState)[keyof typeof BatchProgressState];
  getCount: (progress: BatchProgressDto, totalStudents: number) => number;
};

export const ENROLLMENT_STAGES: EnrollmentStageDefinition[] = [
  {
    key: "download",
    label: "Downloading Photos",
    state: BatchProgressState.Downloading,
    getCount: (p) => p.downloading,
  },
  {
    key: "validate",
    label: "Validation",
    state: BatchProgressState.Validating,
    getCount: (p) => p.validating,
  },
  {
    key: "embed",
    label: "Generating Embeddings",
    state: BatchProgressState.Embedding,
    getCount: (p) => p.embedding,
  },
  {
    key: "upload",
    label: "Uploading Artifacts",
    state: BatchProgressState.Uploading,
    getCount: (p, total) => {
      const accounted =
        p.completed + p.failed + p.cancelled + p.queued + p.downloading + p.validating + p.embedding;
      return Math.max(0, total - accounted);
    },
  },
  {
    key: "complete",
    label: "Completed",
    state: BatchProgressState.Completed,
    getCount: (p) => p.completed,
  },
];

export const getCurrentStageIndex = (state: number): number => {
  const idx = ENROLLMENT_STAGES.findIndex((s) => s.state === state);
  if (idx >= 0) return idx;
  if (state === BatchProgressState.Queued) return 0;
  if (state === BatchProgressState.Failed || state === BatchProgressState.Cancelled) {
    return ENROLLMENT_STAGES.length - 1;
  }
  return 0;
};

export const getStagePercentage = (count: number, total: number): number => {
  if (total <= 0) return 0;
  return Math.min(100, Math.round((count / total) * 100));
};

export type TimelineEvent = {
  id: string;
  title: string;
  timestamp: string | null;
  duration: string | null;
  studentCount: number | null;
  severity: "info" | "warning" | "error" | "success";
  detail?: string;
};

export const buildTimelineEvents = (
  progress: BatchProgressDto | undefined,
  batch: {
    createdUtc: string;
    startedUtc: string | null;
    completedUtc: string | null;
    totalStudents: number;
    failedCount: number;
  },
): TimelineEvent[] => {
  const events: TimelineEvent[] = [
    {
      id: "created",
      title: "Batch Created",
      timestamp: batch.createdUtc,
      duration: null,
      studentCount: batch.totalStudents,
      severity: "info",
    },
  ];

  if (batch.startedUtc) {
    events.push({
      id: "started",
      title: "Download Started",
      timestamp: batch.startedUtc,
      duration: null,
      studentCount: progress?.downloading ?? null,
      severity: "info",
    });
  }

  if (progress && progress.downloading === 0 && progress.validating + progress.embedding > 0) {
    events.push({
      id: "download-complete",
      title: "Download Completed",
      timestamp: null,
      duration: null,
      studentCount: batch.totalStudents - (progress.queued + progress.failed),
      severity: "success",
    });
  }

  if (progress && progress.embedding > 0) {
    events.push({
      id: "embedding-started",
      title: "Embedding Started",
      timestamp: null,
      duration: null,
      studentCount: progress.embedding,
      severity: "info",
    });
  }

  if (progress && progress.completed > 0) {
    events.push({
      id: "embedding-complete",
      title: "Embedding Completed",
      timestamp: null,
      duration: null,
      studentCount: progress.completed,
      severity: "success",
    });
    events.push({
      id: "upload-started",
      title: "Upload Started",
      timestamp: null,
      duration: null,
      studentCount: progress.completed,
      severity: "info",
    });
    events.push({
      id: "validation-complete",
      title: "Validation Completed",
      timestamp: null,
      duration: null,
      studentCount: progress.completed,
      severity: "success",
    });
  }

  if (batch.completedUtc) {
    events.push({
      id: "finished",
      title: "Batch Finished",
      timestamp: batch.completedUtc,
      duration: null,
      studentCount: progress?.completed ?? batch.totalStudents - batch.failedCount,
      severity: batch.failedCount > 0 ? "warning" : "success",
      detail: batch.failedCount > 0 ? `${batch.failedCount} failed` : undefined,
    });
  }

  if (progress && progress.failed > 0) {
    events.push({
      id: "failures",
      title: "Retry Events",
      timestamp: null,
      duration: null,
      studentCount: progress.failed,
      severity: "error",
      detail: "Some students require retry or manual review.",
    });
  }

  return events;
};
