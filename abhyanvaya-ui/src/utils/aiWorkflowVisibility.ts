import { AIStatus, AIWorkflowStep, getWorkflowStepIndex } from "../types/aiWorkflow";

export type RecognitionQueuePhase =
  | "waiting-for-upload"
  | "queued"
  | "processing"
  | "completed"
  | "failed";

export type RecognitionQueueDisplay = {
  phase: RecognitionQueuePhase;
  headline: string;
  subline?: string;
};

const QUEUE_HEADLINES: Record<RecognitionQueuePhase, string> = {
  "waiting-for-upload": "Waiting for Upload",
  queued: "Queued",
  processing: "Processing",
  completed: "Completed",
  failed: "Failed",
};

export const resolveRecognitionQueueDisplay = (
  status: AIStatus,
  recognitionQueued: boolean,
): RecognitionQueueDisplay => {
  if (status === AIStatus.Failed) {
    return {
      phase: "failed",
      headline: QUEUE_HEADLINES.failed,
      subline: "Upload or recognition failed",
    };
  }

  if (
    status === AIStatus.Completed ||
    status === AIStatus.AwaitingReview
  ) {
    return {
      phase: "completed",
      headline: QUEUE_HEADLINES.completed,
      subline: "Recognition pipeline finished",
    };
  }

  if (status === AIStatus.Processing || status === AIStatus.Matching) {
    return {
      phase: "processing",
      headline: QUEUE_HEADLINES.processing,
      subline: "Worker is analyzing the classroom photo",
    };
  }

  if (
    status === AIStatus.Pending ||
    recognitionQueued ||
    status === AIStatus.Uploading
  ) {
    if (status === AIStatus.Uploading) {
      return {
        phase: "waiting-for-upload",
        headline: QUEUE_HEADLINES["waiting-for-upload"],
        subline: "Upload in progress",
      };
    }

    return {
      phase: "queued",
      headline: QUEUE_HEADLINES.queued,
      subline: "Waiting for recognition worker",
    };
  }

  return {
    phase: "waiting-for-upload",
    headline: QUEUE_HEADLINES["waiting-for-upload"],
    subline: "Upload a classroom photo to begin",
  };
};

export type WorkflowSectionKey = "processing" | "review" | "finalize";

/** Progressive disclosure — reveal workflow sections as the session advances. */
export const getVisibleWorkflowSectionKeys = (
  status: AIStatus,
  workflowStep: AIWorkflowStep,
): WorkflowSectionKey[] => {
  const visible: WorkflowSectionKey[] = [];
  const stepIndex = getWorkflowStepIndex(workflowStep);

  const isPostUpload =
    status === AIStatus.Pending ||
    status === AIStatus.Processing ||
    status === AIStatus.Matching ||
    status === AIStatus.AwaitingReview ||
    status === AIStatus.Completed ||
    status === AIStatus.Failed ||
    stepIndex > getWorkflowStepIndex(AIWorkflowStep.Upload);

  if (isPostUpload) {
    visible.push("processing");
  }

  const isPostRecognition =
    status === AIStatus.AwaitingReview ||
    status === AIStatus.Completed ||
    stepIndex >= getWorkflowStepIndex(AIWorkflowStep.Review);

  if (isPostRecognition) {
    visible.push("review");
  }

  const isPostReview =
    status === AIStatus.Completed ||
    workflowStep === AIWorkflowStep.Finalize;

  if (isPostReview) {
    visible.push("finalize");
  }

  return visible;
};

export const WORKFLOW_SECTION_COPY: Record<
  WorkflowSectionKey,
  { title: string; description: string }
> = {
  processing: {
    title: "Processing Status",
    description: "AI is detecting and matching faces in the classroom photo.",
  },
  review: {
    title: "Recognition Review",
    description: "Review recognized students and confirm attendance matches.",
  },
  finalize: {
    title: "Finalize Attendance",
    description: "Approve and save official attendance for this session.",
  },
};
