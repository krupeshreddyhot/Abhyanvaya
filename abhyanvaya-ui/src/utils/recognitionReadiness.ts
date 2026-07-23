import { AIStatus } from "../types/aiWorkflow";
import { SESSION_IMAGE_STATUS } from "../types/sessionImage";
import { AttendanceSessionStatusCode } from "./attendanceSessionStatus";
import { BackendRecognitionQueueStatus } from "../types/liveSessionStatus";

export type RecognitionReadinessState =
  | "WaitingForImages"
  | "NotReady"
  | "ReadyForRecognition"
  | "RecognitionComplete"
  | "RecognitionFailed";

export type RecognitionReadinessView = {
  state: RecognitionReadinessState;
  label: string;
  description: string;
  tone: "default" | "info" | "success" | "warning" | "error";
};

export type RecognitionReadinessInput = {
  imageCount: number;
  status: AIStatus;
  sessionStatusCode?: number | null;
  recognitionQueued?: boolean;
  queueStatus?: number | null;
  hasFailedImages?: boolean;
};

export const getRecognitionReadiness = (
  input: RecognitionReadinessInput,
): RecognitionReadinessView => {
  const {
    imageCount,
    status,
    sessionStatusCode,
    recognitionQueued,
    queueStatus,
    hasFailedImages,
  } = input;

  if (
    status === AIStatus.Failed ||
    sessionStatusCode === AttendanceSessionStatusCode.Failed ||
    queueStatus === BackendRecognitionQueueStatus.Failed ||
    hasFailedImages
  ) {
    return {
      state: "RecognitionFailed",
      label: "Recognition Failed",
      description: "Recognition did not complete. You can retry recognition for this session.",
      tone: "error",
    };
  }

  if (
    status === AIStatus.AwaitingReview ||
    status === AIStatus.Completed ||
    sessionStatusCode === AttendanceSessionStatusCode.AwaitingReview ||
    sessionStatusCode === AttendanceSessionStatusCode.Approved ||
    sessionStatusCode === AttendanceSessionStatusCode.Completed ||
    queueStatus === BackendRecognitionQueueStatus.AwaitingReview ||
    queueStatus === BackendRecognitionQueueStatus.Completed
  ) {
    return {
      state: "RecognitionComplete",
      label: "Recognition Complete",
      description: "Results are ready for teacher review.",
      tone: "success",
    };
  }

  if (imageCount <= 0) {
    return {
      state: "WaitingForImages",
      label: "Waiting for Images",
      description: "Upload or capture at least one classroom photo to begin.",
      tone: "default",
    };
  }

  if (
    status === AIStatus.Uploading ||
    status === AIStatus.Processing ||
    status === AIStatus.Matching ||
    status === AIStatus.Pending ||
    recognitionQueued ||
    (queueStatus != null &&
      queueStatus >= BackendRecognitionQueueStatus.Queued &&
      queueStatus <= BackendRecognitionQueueStatus.Saving)
  ) {
    return {
      state: "ReadyForRecognition",
      label: "Ready for Recognition",
      description: "Images are queued or being processed by AI recognition.",
      tone: "info",
    };
  }

  if (status === AIStatus.Ready || status === AIStatus.NotStarted) {
    return {
      state: "NotReady",
      label: "Not Ready",
      description: "Add or confirm classroom photos, then recognition will start automatically.",
      tone: "warning",
    };
  }

  return {
    state: "ReadyForRecognition",
    label: "Ready for Recognition",
    description: "Session images are available for recognition.",
    tone: "info",
  };
};

/** R2 enterprise status presentation mapped from existing SESSION_IMAGE_STATUS. */
export type EnterpriseImageStatusView = {
  key: "Waiting" | "Queued" | "Processing" | "Processed" | "Failed";
  label: string;
  color: "default" | "info" | "warning" | "success" | "error" | "primary";
  bgcolor: string;
};

export const getEnterpriseImageStatus = (status: number): EnterpriseImageStatusView => {
  switch (status) {
    case SESSION_IMAGE_STATUS.Processing:
      return { key: "Processing", label: "Processing", color: "warning", bgcolor: "warning.light" };
    case SESSION_IMAGE_STATUS.Processed:
      return { key: "Processed", label: "Processed", color: "success", bgcolor: "success.light" };
    case SESSION_IMAGE_STATUS.Failed:
      return { key: "Failed", label: "Failed", color: "error", bgcolor: "error.light" };
    case SESSION_IMAGE_STATUS.Uploaded:
    default:
      return { key: "Waiting", label: "Waiting", color: "default", bgcolor: "action.hover" };
  }
};
