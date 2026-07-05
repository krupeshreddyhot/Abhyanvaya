export const UploadValidationStatus = {
  Idle: "Idle",
  Validating: "Validating",
  Valid: "Valid",
  Invalid: "Invalid",
} as const;

export type UploadValidationStatus =
  (typeof UploadValidationStatus)[keyof typeof UploadValidationStatus];

export const PreviewStatus = {
  None: "None",
  Loading: "Loading",
  Ready: "Ready",
  Failed: "Failed",
} as const;

export type PreviewStatus = (typeof PreviewStatus)[keyof typeof PreviewStatus];

export const UploadStatus = {
  Idle: "Idle",
  Uploading: "Uploading",
  Completed: "Completed",
  Failed: "Failed",
  Cancelled: "Cancelled",
  Retrying: "Retrying",
} as const;

export type UploadStatus = (typeof UploadStatus)[keyof typeof UploadStatus];

export interface UploadState {
  selectedFile?: File;
  previewUrl?: string;
  validationStatus: UploadValidationStatus;
  previewStatus: PreviewStatus;
  uploadStatus: UploadStatus;
  progress: number;
  fileName?: string;
  fileSize?: number;
  imageWidth?: number;
  imageHeight?: number;
  bytesUploaded?: number;
  bytesTotal?: number;
  retryAttempt?: number;
  maxRetries?: number;
  errorMessage?: string;
  uploadedAt?: Date;
}

export const createInitialUploadState = (): UploadState => ({
  validationStatus: UploadValidationStatus.Idle,
  previewStatus: PreviewStatus.None,
  uploadStatus: UploadStatus.Idle,
  progress: 0,
  maxRetries: 3,
});

export const isUploadBusy = (state: UploadState): boolean =>
  state.validationStatus === UploadValidationStatus.Validating ||
  state.uploadStatus === UploadStatus.Uploading ||
  state.uploadStatus === UploadStatus.Retrying;

export const uploadStateToMediaUploadProps = (state: UploadState) => ({
  previewUrl: state.previewUrl ?? null,
  uploading: isUploadBusy(state),
  uploadProgress: isUploadBusy(state) || state.uploadStatus === UploadStatus.Completed ? state.progress : null,
  error: state.errorMessage ?? null,
});
