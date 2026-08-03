import {
  ApprovalDecision,
  ScheduleVersionStatus,
  TimetableApprovalRequestStatus,
  TimetableChangeOperation,
  TimetableCloneJobStatus,
  TimetableCloneJobType,
} from "../../../../services/schedulingService";

export const SCHEDULE_VERSION_STATUS_LABELS: Record<number, string> = {
  [ScheduleVersionStatus.Draft]: "Draft",
  [ScheduleVersionStatus.UnderReview]: "Under review",
  [ScheduleVersionStatus.Approved]: "Approved",
  [ScheduleVersionStatus.Published]: "Published",
  [ScheduleVersionStatus.Archived]: "Archived",
};

export const SCHEDULE_VERSION_STATUS_COLORS: Record<number, "default" | "warning" | "info" | "success" | "error"> = {
  [ScheduleVersionStatus.Draft]: "warning",
  [ScheduleVersionStatus.UnderReview]: "info",
  [ScheduleVersionStatus.Approved]: "success",
  [ScheduleVersionStatus.Published]: "success",
  [ScheduleVersionStatus.Archived]: "default",
};

export const APPROVAL_REQUEST_STATUS_LABELS: Record<number, string> = {
  [TimetableApprovalRequestStatus.Pending]: "Pending",
  [TimetableApprovalRequestStatus.InReview]: "In review",
  [TimetableApprovalRequestStatus.Approved]: "Approved",
  [TimetableApprovalRequestStatus.Rejected]: "Rejected",
  [TimetableApprovalRequestStatus.Returned]: "Returned",
  [TimetableApprovalRequestStatus.Cancelled]: "Cancelled",
};

export const APPROVAL_DECISION_LABELS: Record<number, string> = {
  [ApprovalDecision.Approved]: "Approve",
  [ApprovalDecision.Rejected]: "Reject",
  [ApprovalDecision.Returned]: "Return",
};

export const CLONE_JOB_TYPE_LABELS: Record<number, string> = {
  [TimetableCloneJobType.Day]: "Day",
  [TimetableCloneJobType.Week]: "Week",
  [TimetableCloneJobType.Semester]: "Semester",
  [TimetableCloneJobType.AcademicYear]: "Academic year",
  [TimetableCloneJobType.Department]: "Department",
  [TimetableCloneJobType.Course]: "Course",
  [TimetableCloneJobType.Group]: "Group",
  [TimetableCloneJobType.Faculty]: "Faculty",
  [TimetableCloneJobType.Room]: "Room",
};

export const CLONE_JOB_STATUS_LABELS: Record<number, string> = {
  [TimetableCloneJobStatus.Queued]: "Queued",
  [TimetableCloneJobStatus.Running]: "Running",
  [TimetableCloneJobStatus.Completed]: "Completed",
  [TimetableCloneJobStatus.Failed]: "Failed",
};

export const CHANGE_OPERATION_LABELS: Record<number, string> = {
  [TimetableChangeOperation.Create]: "Create",
  [TimetableChangeOperation.Update]: "Update",
  [TimetableChangeOperation.Delete]: "Delete",
  [TimetableChangeOperation.Move]: "Move",
  [TimetableChangeOperation.Copy]: "Copy",
  [TimetableChangeOperation.Clone]: "Clone",
  [TimetableChangeOperation.Publish]: "Publish",
  [TimetableChangeOperation.Archive]: "Archive",
  [TimetableChangeOperation.Lock]: "Lock",
  [TimetableChangeOperation.Unlock]: "Unlock",
  [TimetableChangeOperation.Freeze]: "Freeze",
  [TimetableChangeOperation.Unfreeze]: "Unfreeze",
};
