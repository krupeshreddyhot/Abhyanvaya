/** Backend AiWorkflowStep numeric values. */
export const BackendWorkflowStep = {
  Upload: 0,
  Detect: 1,
  Match: 2,
  Review: 3,
  Finalize: 4,
} as const;

/** Backend RecognitionQueueStatus numeric values. */
export const BackendRecognitionQueueStatus = {
  Waiting: 0,
  Queued: 1,
  WorkerPicked: 2,
  Detecting: 3,
  Matching: 4,
  Saving: 5,
  AwaitingReview: 6,
  Completed: 7,
  Failed: 8,
  Cancelled: 9,
} as const;

export type AttendanceSessionStatusResponse = {
  attendanceSessionId: string;
  status: number;
  workflowStep: number;
  recognitionQueueStatus: number;
  detectedFaces: number;
  matchedFaces: number;
  reviewedFaces: number;
  recognitionAccuracy?: number | null;
  startedUtc?: string | null;
  lastUpdatedUtc: string;
  elapsedMilliseconds?: number | null;
  recognitionProgressPercent: number;
  currentStage?: string | null;
  currentOperation?: string | null;
  estimatedRemainingMilliseconds?: number | null;
  currentFileName?: string | null;
  messages?: string[];
  errorCode?: string | null;
  processingError?: string | null;
};

export type RecognitionActivityEntry = {
  id: string;
  timestamp: Date;
  message: string;
};
