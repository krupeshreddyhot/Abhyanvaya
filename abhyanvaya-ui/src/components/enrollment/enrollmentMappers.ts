import type { AiSystemStatusItem, AiSystemStatusLevel } from "../ai/AiSystemStatusCard";
import type { EnrollmentSystemStatusDto } from "../../types/enrollment";

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
