import type { AttendanceMethodMode } from "../types/attendanceContext";
import { AIWorkflowStep } from "../types/aiWorkflow";

export const PERIOD_OPTIONS = Array.from({ length: 8 }, (_, index) => ({
  value: index + 1,
  label: `Period ${index + 1}`,
}));

export const ATTENDANCE_METHOD_OPTIONS: ReadonlyArray<{
  value: AttendanceMethodMode;
  label: string;
}> = [
  { value: "manual", label: "Manual Attendance" },
  { value: "aiPhoto", label: "AI Photo Attendance" },
];

export const AI_ATTENDANCE_WORKFLOW_STEPS = [
  {
    key: AIWorkflowStep.Upload,
    shortLabel: "Upload",
    fullLabel: "Upload classroom photograph",
  },
  {
    key: AIWorkflowStep.Detect,
    shortLabel: "Detect",
    fullLabel: "AI detects all faces",
  },
  {
    key: AIWorkflowStep.Match,
    shortLabel: "Match",
    fullLabel: "AI matches students",
  },
  {
    key: AIWorkflowStep.Review,
    shortLabel: "Review",
    fullLabel: "Teacher verifies recognition",
  },
  {
    key: AIWorkflowStep.Finalize,
    shortLabel: "Finalize",
    fullLabel: "Attendance is finalized",
  },
] as const;

export const AI_WORKFLOW_PLACEHOLDER_SECTIONS = [
  {
    key: "upload",
    title: "Upload Area",
    description: "Upload classroom photo",
    minHeight: 120,
  },
  {
    key: "processing",
    title: "Processing Status",
    description: "AI processing status",
    minHeight: 120,
  },
  {
    key: "review",
    title: "Recognition Review",
    description: "Teacher review",
    minHeight: 120,
  },
  {
    key: "summary",
    title: "Recognition Summary",
    description: "Recognition statistics",
    minHeight: 120,
  },
  {
    key: "finalize",
    title: "Finalize Attendance",
    description: "Finalize attendance",
    minHeight: 120,
  },
] as const;
