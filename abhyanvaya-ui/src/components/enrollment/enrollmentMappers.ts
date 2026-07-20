import type { AiSystemStatusItem, AiSystemStatusLevel } from "../ai/AiSystemStatusCard";
import type { BatchProgressDto, BatchSummary, EnrollmentSystemStatusDto } from "../../types/enrollment";

const mapHealthStatus = (status: string): AiSystemStatusLevel => {
  const normalized = status.toLowerCase();
  if (normalized.includes("ready") || normalized.includes("live")) return "ready";
  if (normalized.includes("start") || normalized.includes("degraded")) return "starting";
  if (normalized.includes("offline") || normalized.includes("fail")) return "offline";
  return "unknown";
};

export const mapSystemStatusItems = (status: EnrollmentSystemStatusDto): AiSystemStatusItem[] => [
  {
    label: "Photo Provider",
    detail: status.photoProvider,
    status: mapHealthStatus(status.photoProviderStatus),
  },
  {
    label: "Embedding Engine",
    detail: status.embeddingEngine,
    status: mapHealthStatus(status.embeddingEngineStatus),
  },
  {
    label: "Recognition Engine",
    detail: status.recognitionEngine,
    status: mapHealthStatus(status.recognitionEngineStatus),
  },
  {
    label: "Cloudflare R2",
    detail: status.storageProvider,
    status: mapHealthStatus(status.storageStatus),
  },
  {
    label: "Worker Status",
    status: mapHealthStatus(status.workerStatus),
    statusLabel: status.workerStatus,
  },
];

export const formatDuration = (isoDuration: string | null | undefined): string => {
  if (!isoDuration) return "—";
  const match = /(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})/.exec(isoDuration);
  if (!match) return isoDuration;
  const hours = Number(match[2]);
  const minutes = Number(match[3]);
  if (hours > 0) return `${hours}h ${minutes}m`;
  if (minutes > 0) return `${minutes}m`;
  return "< 1m";
};

export const batchStatusLabel = (status: number): string => {
  switch (status) {
    case 0:
      return "Queued";
    case 1:
      return "Running";
    case 2:
      return "Completed";
    case 3:
      return "Partially Failed";
    case 4:
      return "Cancelled";
    default:
      return "Unknown";
  }
};

/** Processed = completed + failed + cancelled (matches backend CompletionPercentage). */
export const computeBatchProgressPercent = (
  progress: Pick<BatchProgressDto, "completed" | "failed" | "cancelled">,
  totalStudents: number,
): number => {
  if (totalStudents <= 0) return 0;
  const terminal = progress.completed + progress.failed + progress.cancelled;
  return Math.min(100, Math.round((terminal / totalStudents) * 100));
};

export const resolveBatchProgressPercent = (
  batch: Pick<BatchSummary, "totalStudents" | "progressPercent">,
  progress?: BatchProgressDto,
): number => {
  if (progress) {
    return computeBatchProgressPercent(progress, batch.totalStudents);
  }
  return Math.min(100, Math.round(batch.progressPercent));
};

export const resolveBatchStudentCounts = (
  batch: Pick<BatchSummary, "completedCount" | "failedCount" | "totalStudents" | "uploadedWithoutEmbedding">,
  progress?: BatchProgressDto,
) => {
  const photoOnly = progress?.uploadedWithoutEmbedding ?? batch.uploadedWithoutEmbedding ?? 0;

  if (!progress) {
    const processed = batch.completedCount + batch.failedCount;
    return {
      processed,
      completed: batch.completedCount,
      failed: batch.failedCount,
      photoOnly,
      label: `${processed}/${batch.totalStudents}`,
    };
  }

  const processed = progress.completed + progress.failed + progress.cancelled;
  return {
    processed,
    completed: progress.completed,
    failed: progress.failed,
    photoOnly,
    label: `${processed}/${batch.totalStudents}`,
  };
};
