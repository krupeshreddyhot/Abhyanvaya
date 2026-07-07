export const AIWorkflowStep = {
  Upload: "Upload",
  Detect: "Detect",
  Match: "Match",
  Review: "Review",
  Finalize: "Finalize",
} as const;

export type AIWorkflowStep = (typeof AIWorkflowStep)[keyof typeof AIWorkflowStep];

export const AIStatus = {
  Ready: "Ready",
  Uploading: "Uploading",
  Processing: "Processing",
  Matching: "Matching",
  AwaitingReview: "AwaitingReview",
  Completed: "Completed",
  Failed: "Failed",
  Cancelled: "Cancelled",
  Pending: "Pending",
  NotStarted: "NotStarted",
  NotCreated: "NotCreated",
} as const;

export type AIStatus = (typeof AIStatus)[keyof typeof AIStatus];

export const AI_STATUS_LABELS: Record<AIStatus, string> = {
  [AIStatus.Ready]: "Ready",
  [AIStatus.Uploading]: "Uploading",
  [AIStatus.Processing]: "Processing",
  [AIStatus.Matching]: "Matching",
  [AIStatus.AwaitingReview]: "Awaiting Review",
  [AIStatus.Completed]: "Completed",
  [AIStatus.Failed]: "Failed",
  [AIStatus.Cancelled]: "Cancelled",
  [AIStatus.Pending]: "Pending",
  [AIStatus.NotStarted]: "Not Started",
  [AIStatus.NotCreated]: "Not Created",
};

export const AI_WORKFLOW_STEP_SEQUENCE: readonly AIWorkflowStep[] = [
  AIWorkflowStep.Upload,
  AIWorkflowStep.Detect,
  AIWorkflowStep.Match,
  AIWorkflowStep.Review,
  AIWorkflowStep.Finalize,
];

export const getWorkflowStepIndex = (step: AIWorkflowStep): number =>
  AI_WORKFLOW_STEP_SEQUENCE.indexOf(step);
