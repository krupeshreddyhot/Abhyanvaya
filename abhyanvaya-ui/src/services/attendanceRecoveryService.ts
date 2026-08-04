import api from "../api/axios";

export type PendingAttendanceSession = {
  sessionId: string;
  resumeToken: string;
  status: number;
  workflowStatus: number;
  workflowStatusName: string;
  friendlyWorkflowLabel?: string;
  attendanceDate: string;
  courseId: number;
  courseName?: string | null;
  groupId: number;
  groupName?: string | null;
  semesterId: number;
  semesterName?: string | null;
  subjectId: number;
  subjectName?: string | null;
  displayTitle?: string;
  scheduledTimeLabel?: string;
  periodNumber?: number | null;
  staffId?: number | null;
  staffName?: string | null;
  startedUtc?: string | null;
  lastActivityUtc?: string | null;
  elapsedMinutes: number;
  ageMinutes?: number;
  retryCount: number;
  failureCount?: number;
  failureReason?: string | null;
  resumePath: string;
  isExpired: boolean;
  currentStage: string;
  priorityScore?: number;
  priorityBand?: string;
  expectedRemainingMinutes?: number;
  canResume?: boolean;
  canRetry?: boolean;
  canFinalize?: boolean;
  canCancel?: boolean;
  slaLevel?: string;
  slaStatus?: string;
  slaBadgeColor?: string;
  elapsedDisplay?: string;
  expectedCompletionUtc?: string | null;
};

export type PendingAttendanceBucket = {
  myPendingSessions: PendingAttendanceSession[];
  todaysPending: PendingAttendanceSession[];
  reviewPending: PendingAttendanceSession[];
  recognitionRunning: PendingAttendanceSession[];
  failedSessions: PendingAttendanceSession[];
  readyToFinalize: PendingAttendanceSession[];
  totalPending: number;
};

export type PendingSessionQueue = {
  items: PendingAttendanceSession[];
  total: number;
  failedCount: number;
  needsReviewCount: number;
  recognitionReadyCount: number;
  recognitionRunningCount: number;
  sortedByPriority: boolean;
};

export type AttendanceResumeCheckpoint = {
  sessionId: string;
  currentImageId?: string | null;
  zoom?: number | null;
  filtersJson?: string | null;
  currentStudentId?: number | null;
  reviewPosition?: number | null;
  currentBatchId?: string | null;
  resumePath: string;
  workflowStatus: number;
  autoStartRecognition: boolean;
};

export type AutoResumePrompt = {
  shouldPrompt: boolean;
  session?: PendingAttendanceSession | null;
  message: string;
};

export type AttendanceRecoveryDashboard = {
  todayCount: number;
  yesterdayCount: number;
  processingCount: number;
  failedCount: number;
  reviewPendingCount: number;
  finalizationPendingCount: number;
  expiredCount: number;
  sessions: PendingAttendanceSession[];
  byStatus: { label: string; value: number }[];
};

export type AttendanceRecoveryAnalytics = {
  pendingSessions: number;
  averageReviewMinutes?: number | null;
  averageFinalizationMinutes?: number | null;
  averageRetryCount: number;
  failureRatePercent: number;
  recognitionSuccessPercent: number;
  reviewCompletionPercent: number;
  pendingTrend: { label: string; value: number }[];
  facultyProductivity: { label: string; value: number }[];
};

export type AttendanceRecoveryPreference = {
  staffId: number;
  autoSaveFrequencySeconds: number;
  resumeConfirmation: boolean;
  defaultLandingPage: string;
  notificationsEnabled: boolean;
  sessionTimeoutWarning: boolean;
  sessionTimeoutWarningMinutes: number;
  promptOnLogin: boolean;
};

export type FacultyRecoveryCenter = {
  todaysSessions: PendingAttendanceSession[];
  yesterday: PendingAttendanceSession[];
  needsAttention: PendingAttendanceSession[];
  completed: PendingAttendanceSession[];
  archived: PendingAttendanceSession[];
  searchResults: PendingAttendanceSession[];
};

export type AttendanceOperationsDashboard = {
  sessionsByStatus: { label: string; value: number }[];
  longestRunningSessions: PendingAttendanceSession[];
  facultyProductivity: { label: string; value: number }[];
  averageReviewTimeMinutes?: number | null;
  recognitionFailureRatePercent: number;
  retrySuccessRatePercent: number;
  finalizationSlaPercent: number;
  departmentDistribution: { label: string; value: number }[];
  roomDistribution: { label: string; value: number }[];
  topBusyFaculty: { label: string; value: number }[];
};

export type AttendanceOperationalAnalytics = {
  averageRecognitionMinutes?: number | null;
  averageReviewMinutes?: number | null;
  averageFinalizationMinutes?: number | null;
  sessionsStarted: number;
  sessionsCompleted: number;
  retryPercent: number;
  failurePercent: number;
  resumePercent: number;
  peakUsageLabel?: string | null;
  dailyTrends: { label: string; value: number }[];
  departmentTrends: { label: string; value: number }[];
  facultyTrends: { label: string; value: number }[];
  readOnly: boolean;
};

export type AttendanceHealthSnapshot = {
  alerts: {
    code: string;
    severity: string;
    message: string;
    sessionId?: string | null;
    staffId?: number | null;
    detectedUtc: string;
  }[];
  recognitionStalled: number;
  reviewStalled: number;
  abandoned: number;
  repeatedFailures: number;
  largePendingQueues: number;
  longRunning: number;
  neverAutoCancels: boolean;
};

export type FacultyWorkspaceRecoverySummary = {
  todaysClasses: number;
  pendingAttendance: number;
  needsReview: number;
  recognitionRunning: number;
  completed: number;
  completedToday?: number;
  averageReviewTimeMinutes?: number | null;
  pendingByPriority?: { label: string; value: number }[];
  slaDistribution?: { label: string; value: number }[];
  topPending: PendingAttendanceSession[];
};

export type SessionTimelineEvent = {
  operation: string;
  occurredUtc: string;
  relativeTime: string;
  userId?: number | null;
  userDisplay?: string | null;
  reason?: string | null;
  success: boolean;
  source: string;
};

export type SessionTimeline = {
  sessionId: string;
  events: SessionTimelineEvent[];
  total: number;
  page: number;
  pageSize: number;
  reusesRetryHistory: boolean;
};

export type DepartmentOperationsSummary = {
  departmentId: number;
  departmentName: string;
  departmentCode?: string | null;
  pendingSessions: number;
  completed: number;
  failed: number;
  recognitionRunning: number;
  needsReview: number;
  averageCompletionMinutes?: number | null;
  averageRecognitionMinutes?: number | null;
  facultyCount: number;
};

export type DepartmentOperationsDashboard = {
  departments: DepartmentOperationsSummary[];
  pendingTrend: { label: string; value: number }[];
  completionTrend: { label: string; value: number }[];
  reusesCatalogDepartment: boolean;
};

export type EnterpriseOpsDashboard = {
  slaDistribution: { label: string; value: number }[];
  departmentSummary: DepartmentOperationsSummary[];
  topDelayedSessions: PendingAttendanceSession[];
  facultySla: { label: string; value: number }[];
  averageReviewTimeMinutes?: number | null;
  timelineTrends: { label: string; value: number }[];
  retrySuccessPercent: number;
  failureTrend: { label: string; value: number }[];
  dailyHeatmap: { label: string; value: number }[];
  departmentHeatmap: { label: string; value: number }[];
};

export const AttendanceBulkOperationKind = {
  NotifyFaculty: 1,
  ArchiveExpired: 2,
  ExportSessions: 3,
  RetryFailedRecognition: 4,
  MarkReviewed: 5,
  CloseCompleted: 6,
} as const;

export type BulkOperationResult = {
  operationId: string;
  operation: string;
  requestedCount: number;
  succeededCount: number;
  skippedCount: number;
  failedCount: number;
  items: { sessionId: string; success: boolean; skipped: boolean; message?: string | null }[];
  neverAutoFinalizes: boolean;
  neverRetriesSuccessful: boolean;
};

export const AttendanceRetryKind = {
  RetryRecognition: 1,
  RetryFailedImages: 2,
  RetryUpload: 3,
  RetryFinalization: 4,
  RetryEntireSession: 5,
} as const;

export const getPendingAttendance = () =>
  api.get<PendingAttendanceBucket>("/attendance-recovery/pending");

export const getPendingSessionQueue = (params?: Record<string, string | number | boolean | undefined>) =>
  api.get<PendingSessionQueue>("/attendance-recovery/queue", { params });

export const cancelRecoverySession = (sessionId: string) =>
  api.post(`/attendance-recovery/sessions/${sessionId}/cancel`);

export const getFacultyRecoveryCenter = (query?: string) =>
  api.get<FacultyRecoveryCenter>("/attendance-recovery/recovery-center", { params: { query } });

export const getWorkspaceRecoverySummary = () =>
  api.get<FacultyWorkspaceRecoverySummary>("/attendance-recovery/workspace-summary");

export const getRecoveryPreferences = () =>
  api.get<AttendanceRecoveryPreference>("/attendance-recovery/preferences");

export const upsertRecoveryPreferences = (payload: Partial<AttendanceRecoveryPreference>) =>
  api.put<AttendanceRecoveryPreference>("/attendance-recovery/preferences", payload);

export const getResumeCheckpoint = (sessionId: string) =>
  api.get<AttendanceResumeCheckpoint>(`/attendance-recovery/sessions/${sessionId}/resume`);

export const saveResumeCheckpoint = (sessionId: string, payload: Partial<AttendanceResumeCheckpoint>) =>
  api.put<AttendanceResumeCheckpoint>(`/attendance-recovery/sessions/${sessionId}/checkpoint`, payload);

export const retryAttendanceSession = (sessionId: string, kind: number, imageId?: string) =>
  api.post(`/attendance-recovery/sessions/${sessionId}/retry`, { kind, imageId: imageId ?? null });

export const getRetryHistory = (sessionId: string) =>
  api.get(`/attendance-recovery/sessions/${sessionId}/retry-history`);

export const getAutoResumePrompt = () => api.get<AutoResumePrompt>("/attendance-recovery/auto-resume");

export const decideAutoResume = (decision: string, sessionId?: string, remember = true) =>
  api.post("/attendance-recovery/auto-resume/decision", { decision, sessionId: sessionId ?? null, remember });

export const searchRecoverySessions = (params: Record<string, string | number | undefined>) =>
  api.get<PendingAttendanceSession[]>("/attendance-recovery/search", { params });

export const getAdminRecoveryDashboard = () =>
  api.get<AttendanceRecoveryDashboard>("/admin/attendance-recovery/dashboard");

export const getAdminRecoveryAnalytics = () =>
  api.get<AttendanceRecoveryAnalytics>("/admin/attendance-recovery/analytics");

export const getAdminOperationsDashboard = () =>
  api.get<AttendanceOperationsDashboard>("/admin/attendance-recovery/operations");

export const getAdminOperationalAnalytics = () =>
  api.get<AttendanceOperationalAnalytics>("/admin/attendance-recovery/operational-analytics");

export const getAdminHealthSnapshot = () =>
  api.get<AttendanceHealthSnapshot>("/admin/attendance-recovery/health");

export const exportAdminRecoveryCsv = () =>
  api.get("/admin/attendance-recovery/export", { responseType: "blob" });

export const adminRecoveryAction = (sessionId: string, action: string, reason?: string) =>
  api.post(`/admin/attendance-recovery/sessions/${sessionId}/actions`, { action, reason: reason ?? null });

export const getSessionTimeline = (sessionId: string, page = 1, pageSize = 50) =>
  api.get<SessionTimeline>(`/attendance-recovery/sessions/${sessionId}/timeline`, { params: { page, pageSize } });

export const getAdminDepartmentOperations = () =>
  api.get<DepartmentOperationsDashboard>("/admin/attendance-recovery/department-operations");

export const getAdminEnterpriseOps = () =>
  api.get<EnterpriseOpsDashboard>("/admin/attendance-recovery/enterprise-ops");

export const runAdminBulkOperation = (operation: number, sessionIds: string[], reason?: string) =>
  api.post<BulkOperationResult>("/admin/attendance-recovery/bulk", {
    operation,
    sessionIds,
    reason: reason ?? null,
  });

export const getAdminBulkHistory = (take = 50) =>
  api.get("/admin/attendance-recovery/bulk/history", { params: { take } });
