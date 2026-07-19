import api from "../api/axios";
import type { AttendanceContext } from "../types/attendanceContext";
import { getUploadApiErrorMessage, isRetryableUploadError } from "../utils/apiErrorMessage";
import { mapUploadProgressToMilestone, sleep } from "../utils/uploadProgress";

export type CreatePhotoAttendanceSessionResponse = {
  attendanceSessionId: string;
};

export type ClassroomPhotoUploadResponse = {
  attendanceSessionId: string;
  imageUploaded: boolean;
  uploadUtc: string;
  imageUrl?: string | null;
  queued: boolean;
  imageStorageKey?: string;
};

export type AttendanceSessionReviewResponse = {
  id: string;
  status: number;
  attendanceDate: string;
  annotatedImageUrl?: string | null;
  originalImageUrl?: string | null;
  imageWidth?: number | null;
  imageHeight?: number | null;
};

export type UploadProgressHandler = (progress: {
  milestone: number;
  loaded: number;
  total: number;
}) => void;

const MAX_UPLOAD_RETRIES = 3;
const RETRY_BACKOFF_MS = [1000, 2000, 4000];

export const createPhotoAttendanceSession = async (
  context: AttendanceContext,
  totalStudents: number,
): Promise<CreatePhotoAttendanceSessionResponse> => {
  const response = await api.post<CreatePhotoAttendanceSessionResponse>("/attendance-sessions", {
    courseId: context.courseId,
    groupId: context.groupId,
    semesterId: context.semesterId,
    subjectId: context.subjectId,
    attendanceDate: context.attendanceDate,
    periodNumber: context.periodNumber,
    totalStudents,
  });

  return response.data;
};

export const uploadClassroomPhoto = async (
  sessionId: string,
  file: File,
  options?: {
    signal?: AbortSignal;
    onProgress?: UploadProgressHandler;
    onRetryAttempt?: (attempt: number) => void;
  },
): Promise<ClassroomPhotoUploadResponse> => {
  let lastError: unknown;

  for (let attempt = 0; attempt < MAX_UPLOAD_RETRIES; attempt += 1) {
    if (attempt > 0) {
      options?.onRetryAttempt?.(attempt);
      await sleep(RETRY_BACKOFF_MS[attempt - 1] ?? 4000);
    }

    try {
      const formData = new FormData();
      formData.append("file", file);

      const response = await api.post<ClassroomPhotoUploadResponse>(
        `/attendance-sessions/${sessionId}/classroom-photo`,
        formData,
        {
          headers: { "Content-Type": "multipart/form-data" },
          signal: options?.signal,
          onUploadProgress: (event) => {
            const total = event.total ?? file.size;
            const loaded = event.loaded ?? 0;
            options?.onProgress?.({
              milestone: mapUploadProgressToMilestone(loaded, total),
              loaded,
              total,
            });
          },
        },
      );

      return response.data;
    } catch (error) {
      lastError = error;
      if (options?.signal?.aborted || !isRetryableUploadError(error)) {
        throw new Error(getUploadApiErrorMessage(error));
      }
    }
  }

  throw new Error(getUploadApiErrorMessage(lastError));
};

export const getAttendanceSession = async (
  sessionId: string,
): Promise<AttendanceSessionReviewResponse> => {
  const response = await api.get<AttendanceSessionReviewResponse>(`/attendance-sessions/${sessionId}`);
  return response.data;
};

export const getUploadRetryLabel = (attempt: number): string =>
  attempt <= 0 ? "" : `Retrying upload... Attempt ${attempt + 1} of ${MAX_UPLOAD_RETRIES}`;

export const MAX_UPLOAD_ATTEMPTS = MAX_UPLOAD_RETRIES;
