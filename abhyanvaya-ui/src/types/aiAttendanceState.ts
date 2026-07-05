import { AIStatus, AIWorkflowStep } from "./aiWorkflow";

export interface AiAttendanceState {
  attendanceSessionId?: string;
  recognitionSessionId?: string;
  uploadProgress: number;
  recognitionProgress: number;
  workflowStep: AIWorkflowStep;
  status: AIStatus;
  uploadedImageUrl?: string;
  detectedFaces: number;
  matchedFaces: number;
  reviewedFaces: number;
  recognitionQueued: boolean;
  processingStartTime?: Date | null;
  recognitionQueueStatus?: number;
  recognitionAccuracy?: number | null;
  elapsedMilliseconds?: number | null;
  currentStage?: string;
  currentOperation?: string;
  estimatedRemainingMilliseconds?: number | null;
  currentFileName?: string;
  processingMessages?: string[];
  errorCode?: string;
  processingError?: string;
  lastUpdatedUtc?: Date;
  sessionStatusCode?: number;
}

export const createInitialAiAttendanceState = (): AiAttendanceState => ({
  uploadProgress: 0,
  recognitionProgress: 0,
  workflowStep: AIWorkflowStep.Upload,
  status: AIStatus.Ready,
  detectedFaces: 0,
  matchedFaces: 0,
  reviewedFaces: 0,
  recognitionQueued: false,
  processingStartTime: null,
  recognitionAccuracy: null,
  elapsedMilliseconds: null,
  estimatedRemainingMilliseconds: null,
  processingMessages: [],
});
