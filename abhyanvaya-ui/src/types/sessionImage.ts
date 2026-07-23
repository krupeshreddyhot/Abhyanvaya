/** AI22.7A Phase 2/3 — classroom image collection (1–10 per session). */

export const SESSION_IMAGE_STATUS = {
  Uploaded: 1,
  Processing: 2,
  Processed: 3,
  Failed: 4,
} as const;

export type SessionImageStatus =
  (typeof SESSION_IMAGE_STATUS)[keyof typeof SESSION_IMAGE_STATUS];

export type AttendanceSessionImage = {
  id: string;
  imageSequence: number;
  imageUrl?: string | null;
  originalFileName?: string | null;
  width?: number | null;
  height?: number | null;
  fileSize?: number | null;
  uploadedUtc?: string | null;
  /** Phase 3 — device capture time when available. */
  captureTimestamp?: string | null;
  captureDevice?: string | null;
  captureLatitude?: number | null;
  captureLongitude?: number | null;
  orientation?: number | null;
  acquisitionMethod?: string | null;
  blurScore?: number | null;
  status: SessionImageStatus | number;
  processingError?: string | null;
  imageStorageKey: string;
  detectedFaceCount?: number;
  batchStatus?: string | null;
};

export type ClassroomPhotoCollectionUploadResponse = {
  attendanceSessionId: string;
  image: AttendanceSessionImage;
  queued: boolean;
  imageCount: number;
  recognitionScope?: string | null;
};

export const MAX_CLASSROOM_IMAGES_PER_SESSION = 10;

export const sessionImageStatusLabel = (status: number): string => {
  switch (status) {
    case SESSION_IMAGE_STATUS.Uploaded:
      return "Uploaded";
    case SESSION_IMAGE_STATUS.Processing:
      return "Processing";
    case SESSION_IMAGE_STATUS.Processed:
      return "Processed";
    case SESSION_IMAGE_STATUS.Failed:
      return "Failed";
    default:
      return "Unknown";
  }
};
