import api from "../api/axios";

export type EmbeddingStatus =
  | "Pending"
  | "Processing"
  | "Completed"
  | "Failed"
  | "Inactive";

export type EmbeddingQuality =
  | "Unknown"
  | "Poor"
  | "Fair"
  | "Good"
  | "Excellent";

export type StudentFaceEmbeddingDto = {
  id: string;
  studentId: number;
  embeddingModel: string;
  embeddingVersion: string;
  embeddingStatus: EmbeddingStatus | number;
  embeddingQuality: EmbeddingQuality | number;
  embeddingDimension: number;
  photoVersion: number;
  retryCount: number;
  lastFailureUtc?: string | null;
  lastFailureReason?: string | null;
  photoKey: string;
  generatedUtc: string;
  generatedBy?: number | null;
  isActive: boolean;
  vectorDimensions: number;
};

export type StudentFaceEmbeddingStatusDto = {
  studentId: number;
  hasPhoto: boolean;
  hasActiveEmbedding: boolean;
  activeStatus?: EmbeddingStatus | number | null;
  activeQuality?: EmbeddingQuality | number | null;
  activeModel?: string | null;
  activeVersion?: string | null;
  activeDimension?: number | null;
  activePhotoVersion?: number | null;
  currentPhotoVersion?: number | null;
  isPhotoVersionStale?: boolean;
  generatedUtc?: string | null;
  generationPending: boolean;
  totalEmbeddings: number;
  retryCount: number;
  activeEmbeddingId?: string | null;
};

const embeddingStatusLabel = (value?: EmbeddingStatus | number | null): string => {
  if (value == null) return "—";
  if (typeof value === "number") {
    switch (value) {
      case 0:
        return "Pending";
      case 1:
        return "Processing";
      case 2:
        return "Completed";
      case 3:
        return "Failed";
      case 4:
        return "Inactive";
      default:
        return "Unknown";
    }
  }
  return value;
};

const embeddingQualityLabel = (value?: EmbeddingQuality | number | null): string => {
  if (value == null) return "—";
  if (typeof value === "number") {
    switch (value) {
      case 1:
        return "Poor";
      case 2:
        return "Fair";
      case 3:
        return "Good";
      case 4:
        return "Excellent";
      default:
        return "Unknown";
    }
  }
  return value;
};

export { embeddingQualityLabel, embeddingStatusLabel };

export const getStudentEmbeddingStatus = (studentId: number) =>
  api.get<StudentFaceEmbeddingStatusDto>(`/student/${studentId}/embeddings/status`);

export const listStudentEmbeddings = (studentId: number) =>
  api.get<StudentFaceEmbeddingDto[]>(`/student/${studentId}/embeddings`);

export const generateStudentEmbedding = (studentId: number) =>
  api.post<StudentFaceEmbeddingStatusDto>(`/student/${studentId}/embeddings/generate`);

export const regenerateStudentEmbedding = (studentId: number) =>
  api.post<StudentFaceEmbeddingStatusDto>(`/student/${studentId}/embeddings/regenerate`);

export const deactivateStudentEmbedding = (studentId: number, embeddingId: string) =>
  api.post<StudentFaceEmbeddingDto>(`/student/${studentId}/embeddings/${embeddingId}/deactivate`);
