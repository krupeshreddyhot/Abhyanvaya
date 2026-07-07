import { AIStatus, AIWorkflowStep } from "../types/aiWorkflow";

/** Backend AttendanceSessionStatus numeric values. */
export const AttendanceSessionStatusCode = {
  Draft: 0,
  Pending: 1,
  Processing: 2,
  AwaitingReview: 3,
  Approved: 4,
  Failed: 5,
  Cancelled: 6,
  Completed: 7,
} as const;

export const mapSessionStatusToAiStatus = (statusCode: number): AIStatus => {
  switch (statusCode) {
    case AttendanceSessionStatusCode.Pending:
      return AIStatus.Pending;
    case AttendanceSessionStatusCode.Processing:
      return AIStatus.Processing;
    case AttendanceSessionStatusCode.AwaitingReview:
      return AIStatus.AwaitingReview;
    case AttendanceSessionStatusCode.Failed:
      return AIStatus.Failed;
    case AttendanceSessionStatusCode.Completed:
    case AttendanceSessionStatusCode.Approved:
      return AIStatus.Completed;
    case AttendanceSessionStatusCode.Cancelled:
      return AIStatus.Cancelled;
    case AttendanceSessionStatusCode.Draft:
    default:
      return AIStatus.NotCreated;
  }
};

export const mapSessionStatusToWorkflowStep = (statusCode: number): AIWorkflowStep => {
  switch (statusCode) {
    case AttendanceSessionStatusCode.Processing:
      return AIWorkflowStep.Detect;
    case AttendanceSessionStatusCode.AwaitingReview:
      return AIWorkflowStep.Review;
    case AttendanceSessionStatusCode.Approved:
    case AttendanceSessionStatusCode.Completed:
      return AIWorkflowStep.Finalize;
    case AttendanceSessionStatusCode.Pending:
      return AIWorkflowStep.Upload;
    default:
      return AIWorkflowStep.Upload;
  }
};
