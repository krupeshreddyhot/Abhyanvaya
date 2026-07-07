import type { AiAttendanceState } from "../types/aiAttendanceState";
import type {
  AttendanceSessionStatusResponse,
  RecognitionActivityEntry,
} from "../types/liveSessionStatus";
import { BackendWorkflowStep } from "../types/liveSessionStatus";
import { AIStatus, AIWorkflowStep } from "../types/aiWorkflow";
import { mapSessionStatusToAiStatus } from "../utils/attendanceSessionStatus";

const mapBackendWorkflowStep = (step: number): AIWorkflowStep => {
  switch (step) {
    case BackendWorkflowStep.Detect:
      return AIWorkflowStep.Detect;
    case BackendWorkflowStep.Match:
      return AIWorkflowStep.Match;
    case BackendWorkflowStep.Review:
      return AIWorkflowStep.Review;
    case BackendWorkflowStep.Finalize:
      return AIWorkflowStep.Finalize;
    case BackendWorkflowStep.Upload:
    default:
      return AIWorkflowStep.Upload;
  }
};

export const mapStatusResponseToAiState = (
  response: AttendanceSessionStatusResponse,
  previous?: AiAttendanceState,
): AiAttendanceState => {
  const status = mapSessionStatusToAiStatus(response.status);
  const startedUtc = response.startedUtc ? new Date(response.startedUtc) : null;

  return {
    attendanceSessionId: response.attendanceSessionId,
    recognitionSessionId: response.attendanceSessionId,
    uploadProgress: previous?.uploadProgress ?? (status === AIStatus.Pending ? 100 : 0),
    recognitionProgress: response.recognitionProgressPercent,
    workflowStep: mapBackendWorkflowStep(response.workflowStep),
    status,
    uploadedImageUrl: previous?.uploadedImageUrl,
    detectedFaces: response.detectedFaces,
    matchedFaces: response.matchedFaces,
    reviewedFaces: response.reviewedFaces,
    recognitionQueued:
      response.recognitionQueueStatus >= 1 && response.recognitionQueueStatus <= 5,
    processingStartTime:
      status === AIStatus.Processing || status === AIStatus.AwaitingReview
        ? previous?.processingStartTime ?? startedUtc ?? new Date()
        : previous?.processingStartTime ?? null,
    recognitionQueueStatus: response.recognitionQueueStatus,
    recognitionAccuracy: response.recognitionAccuracy ?? null,
    elapsedMilliseconds: response.elapsedMilliseconds ?? null,
    currentStage: response.currentStage ?? undefined,
    currentOperation: response.currentOperation ?? undefined,
    estimatedRemainingMilliseconds: response.estimatedRemainingMilliseconds ?? null,
    currentFileName: response.currentFileName ?? undefined,
    processingMessages: response.messages ?? [],
    errorCode: response.errorCode ?? undefined,
    processingError: response.processingError ?? undefined,
    lastUpdatedUtc: response.lastUpdatedUtc ? new Date(response.lastUpdatedUtc) : undefined,
    sessionStatusCode: response.status,
  };
};

const formatActivityTime = (date: Date): string =>
  date.toLocaleTimeString(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  });

export const buildActivityEntriesFromStatus = (
  response: AttendanceSessionStatusResponse,
  previous?: AttendanceSessionStatusResponse,
): RecognitionActivityEntry[] => {
  const timestamp = response.lastUpdatedUtc ? new Date(response.lastUpdatedUtc) : new Date();
  const entries: RecognitionActivityEntry[] = [];

  const push = (message: string, key: string) => {
    entries.push({
      id: `${key}-${timestamp.getTime()}`,
      timestamp,
      message,
    });
  };

  if (!previous || previous.recognitionQueueStatus !== response.recognitionQueueStatus) {
    push(response.currentOperation ?? response.currentStage ?? "Status updated", "queue");
  }

  if (!previous || previous.detectedFaces !== response.detectedFaces) {
    if (response.detectedFaces > 0) {
      push(`${response.detectedFaces} face(s) detected`, "detected");
    }
  }

  if (!previous || previous.matchedFaces !== response.matchedFaces) {
    if (response.matchedFaces > 0) {
      push(`${response.matchedFaces} student match(es) found`, "matched");
    }
  }

  if (
    response.status !== previous?.status &&
    (response.status === 3 || response.recognitionQueueStatus === 6)
  ) {
    push("Recognition complete — awaiting teacher review", "review-ready");
  }

  if (response.processingError && response.processingError !== previous?.processingError) {
    push(response.processingError, "error");
  }

  return entries;
};

export const formatRecognitionActivityTime = formatActivityTime;

export const isTerminalSessionStatus = (statusCode: number): boolean =>
  statusCode === 5 || statusCode === 6 || statusCode === 7 || statusCode === 4;

export const isProcessingVisible = (statusCode: number, queueStatus: number): boolean =>
  statusCode === 1 ||
  statusCode === 2 ||
  (queueStatus >= 1 && queueStatus <= 5);

export const isReviewVisible = (statusCode: number): boolean => statusCode === 3;

export const isFinalizeVisible = (statusCode: number): boolean =>
  statusCode === 4 || statusCode === 7;
