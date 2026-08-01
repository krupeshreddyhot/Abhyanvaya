import api from "../api/axios";
import type { AttendanceContext } from "../types/attendanceContext";
import type { ClassroomPhotoCaptureContext } from "../types/photoAcquisition";
import type {
  AttendanceSessionImage,
  ClassroomPhotoCollectionUploadResponse,
} from "../types/sessionImage";
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

export type { AttendanceSessionImage, ClassroomPhotoCollectionUploadResponse };

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
  const response = await api.post<
    CreatePhotoAttendanceSessionResponse & { AttendanceSessionId?: string }
  >("/attendance-sessions", {
    courseId: context.courseId,
    groupId: context.groupId,
    semesterId: context.semesterId,
    subjectId: context.subjectId,
    attendanceDate: context.attendanceDate,
    periodNumber: context.periodNumber,
    totalStudents,
  });

  const sessionId =
    response.data.attendanceSessionId ?? response.data.AttendanceSessionId ?? "";
  if (!sessionId) {
    throw new Error("Server did not return an attendance session id.");
  }

  return { attendanceSessionId: sessionId };
};

export const uploadClassroomPhoto = async (
  sessionId: string,
  file: File,
  options?: {
    signal?: AbortSignal;
    onProgress?: UploadProgressHandler;
    onRetryAttempt?: (attempt: number) => void;
    captureContext?: ClassroomPhotoCaptureContext;
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
      // Include filename so ASP.NET binds IFormFile correctly.
      formData.append("file", file, file.name || "classroom-photo.jpg");
      appendCaptureContext(formData, options?.captureContext);

      // Do NOT set Content-Type manually — the browser must add the multipart boundary.
      const response = await api.post<ClassroomPhotoUploadResponse>(
        `/attendance-sessions/${sessionId}/classroom-photo`,
        formData,
        {
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

const appendCaptureContext = (formData: FormData, ctx?: ClassroomPhotoCaptureContext) => {
  if (!ctx) {
    return;
  }
  if (ctx.acquisitionMethod) {
    formData.append("acquisitionMethod", ctx.acquisitionMethod);
  }
  if (ctx.captureDevice) {
    formData.append("captureDevice", ctx.captureDevice);
  }
  if (ctx.captureTimestampUtc) {
    formData.append("captureTimestampUtc", ctx.captureTimestampUtc);
  }
  if (ctx.orientation != null) {
    formData.append("orientation", String(ctx.orientation));
  }
  if (ctx.latitude != null) {
    formData.append("latitude", String(ctx.latitude));
  }
  if (ctx.longitude != null) {
    formData.append("longitude", String(ctx.longitude));
  }
  if (ctx.blurScore != null) {
    formData.append("blurScore", String(ctx.blurScore));
  }
};

export const listClassroomImages = async (
  sessionId: string,
): Promise<AttendanceSessionImage[]> => {
  const response = await api.get<AttendanceSessionImage[]>(
    `/attendance-sessions/${sessionId}/classroom-images`,
  );
  return response.data;
};

export const deleteClassroomImage = async (
  sessionId: string,
  imageId: string,
): Promise<void> => {
  await api.delete(`/attendance-sessions/${sessionId}/classroom-images/${imageId}`);
};

export const replaceClassroomImage = async (
  sessionId: string,
  imageId: string,
  file: File,
  options?: {
    signal?: AbortSignal;
    onProgress?: UploadProgressHandler;
    onRetryAttempt?: (attempt: number) => void;
    captureContext?: ClassroomPhotoCaptureContext;
  },
): Promise<ClassroomPhotoCollectionUploadResponse> => {
  let lastError: unknown;

  for (let attempt = 0; attempt < MAX_UPLOAD_RETRIES; attempt += 1) {
    if (attempt > 0) {
      options?.onRetryAttempt?.(attempt);
      await sleep(RETRY_BACKOFF_MS[attempt - 1] ?? 4000);
    }

    try {
      const formData = new FormData();
      formData.append("file", file, file.name || "classroom-photo.jpg");
      appendCaptureContext(formData, options?.captureContext);

      // Do NOT set Content-Type manually — the browser must add the multipart boundary.
      const response = await api.put<ClassroomPhotoCollectionUploadResponse>(
        `/attendance-sessions/${sessionId}/classroom-images/${imageId}`,
        formData,
        {
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

export const reorderClassroomImages = async (
  sessionId: string,
  imageIds: string[],
): Promise<AttendanceSessionImage[]> => {
  const response = await api.put<AttendanceSessionImage[]>(
    `/attendance-sessions/${sessionId}/classroom-images/reorder`,
    { imageIds },
  );
  return response.data;
};

export const requeueClassroomRecognition = async (sessionId: string): Promise<void> => {
  await api.post(`/attendance-sessions/${sessionId}/classroom-images/requeue`);
};

/** AI22.7A Phase 3 — retry recognition for one image only. */
export const requeueClassroomImage = async (
  sessionId: string,
  imageId: string,
): Promise<void> => {
  await api.post(`/attendance-sessions/${sessionId}/classroom-images/${imageId}/requeue`);
};

export const getUploadRetryLabel = (attempt: number): string =>
  attempt <= 0 ? "" : `Retrying upload... Attempt ${attempt + 1} of ${MAX_UPLOAD_RETRIES}`;

export const MAX_UPLOAD_ATTEMPTS = MAX_UPLOAD_RETRIES;
