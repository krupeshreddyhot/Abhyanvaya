import api from "../api/axios";

export type FacultyClassDto = {
  timetableEntryId?: number | null;
  timetableId?: number | null;
  status: string;
  dayOfWeek: number;
  timeSlotId?: number | null;
  periodNumber?: number | null;
  startTime?: string | null;
  endTime?: string | null;
  minutesRemaining?: number | null;
  courseId: number;
  groupId: number;
  semesterId: number;
  subjectId: number;
  subjectName?: string | null;
  roomId?: number | null;
  roomName?: string | null;
  buildingName?: string | null;
  floorName?: string | null;
  studentCount?: number | null;
  attendanceStatus: string;
  aiCaptureStatus?: string | null;
  attendanceSessionId?: string | null;
};

export type FacultyTodayDto = {
  date: string;
  staffId?: number | null;
  mode: string;
  hasTimetable: boolean;
  message: string;
  currentClass?: FacultyClassDto | null;
  nextClass?: FacultyClassDto | null;
  todaysSchedule: FacultyClassDto[];
  attendanceSummary: {
    classesToday: number;
    attendanceTaken: number;
    pending: number;
    missed: number;
    presentMarks: number;
    absentMarks: number;
  };
  aiAttendanceSummary: {
    sessionsToday: number;
    pendingReviews: number;
    averageRecognitionAccuracy?: number | null;
    aiUsageCount: number;
  };
  pendingReviews: Array<{
    attendanceSessionId: string;
    label: string;
    pendingCount: number;
    updatedUtc?: string | null;
    reviewPath: string;
  }>;
  notifications: FacultyScheduleNotificationDto[];
  quickActions: FacultyQuickActionDto[];
  generatedUtc: string;
  modifiesAttendanceApis: boolean;
};

export type FacultyQuickActionDto = {
  code: string;
  label: string;
  path: string;
  primary: boolean;
  enabled: boolean;
  hint?: string | null;
};

export type FacultyScheduleNotificationDto = {
  notificationId: string;
  kind: string;
  title: string;
  message: string;
  occurredUtc: string;
  timetableId?: number | null;
  entryId?: number | null;
};

export type FacultyTimetableViewDto = {
  view: string;
  from: string;
  to: string;
  classes: FacultyClassDto[];
};

export type FacultyInsightsDto = {
  attendanceTaken: number;
  pending: number;
  missed: number;
  averageCompletionMinutes?: number | null;
  aiUsage: number;
  recognitionAccuracy?: number | null;
  weekly: { sessions: number; completed: number; aiSessions: number; avgAccuracy?: number | null };
  monthly: { sessions: number; completed: number; aiSessions: number; avgAccuracy?: number | null };
};

export type FacultyCurrentClassWorkspaceDto = {
  currentClass?: FacultyClassDto | null;
  mode: string;
  hasTimetable: boolean;
  message: string;
  quickActions: FacultyQuickActionDto[];
  opensOnlyTodaysActiveClass: boolean;
};

export type FacultyTimelineItemDto = {
  kind: string;
  status: string;
  startTime?: string | null;
  endTime?: string | null;
  label: string;
  subjectName?: string | null;
  roomName?: string | null;
  buildingName?: string | null;
  attendanceStatus: string;
  aiReviewPending: boolean;
  class?: FacultyClassDto | null;
};

export type FacultyTimelineDto = { date: string; items: FacultyTimelineItemDto[]; reusedTodaysSchedule: boolean };

export type ClassroomNavigationDto = {
  roomId: number;
  roomName: string;
  roomCode: string;
  capacity: number;
  roomType: string;
  features: string[];
  accessibilityFriendly: boolean;
  campusName: string;
  buildingName: string;
  floorName: string;
  floorLevel: number;
  walkingEstimateMinutes?: number | null;
  directionsPlaceholder: string;
  usesGis: boolean;
};

export type WorkspacePreferenceDto = {
  id: number;
  staffId: number;
  userId: number;
  landingPage: string;
  dashboardLayout: string;
  defaultTimetableView: string;
  favoriteQuickActions: string[];
  themePreference: string;
  notificationPreferences: Record<string, boolean>;
  oneHandedMode: boolean;
  highContrast: boolean;
  updatedUtc?: string | null;
};

export type FacultyAttendanceProductivityDto = {
  pendingAttendance: number;
  remainingClasses: number;
  attendanceCompletionPercent: number;
  aiPendingReviews: number;
  missedAttendance: number;
  lateAttendance: number;
  quickResumePath?: string | null;
  reusesAttendanceApis: boolean;
};

export type FacultyProductivityDashboardDto = {
  classesToday: number;
  attendanceCompleted: number;
  attendanceRate: number;
  aiUsage: number;
  recognitionAccuracy?: number | null;
  weeklyWorkload: Array<{ label: string; value: number }>;
  monthlyWorkload: Array<{ label: string; value: number }>;
  roomUtilization: Array<{ label: string; value: number }>;
  reusesExistingAnalytics: boolean;
};

export type FacultySearchResponseDto = {
  query: string;
  results: Array<{
    category: string;
    title: string;
    subtitle: string;
    navigationPath: string;
    entityKey?: string | null;
  }>;
  usesElasticsearch: boolean;
};

export type FacultySmartNotificationsDto = {
  items: FacultyScheduleNotificationDto[];
  usesSignalR: boolean;
  usesPolling: boolean;
};

export const getFacultyToday = (params?: { date?: string }) =>
  api.get<FacultyTodayDto>("/faculty/workspace/today", { params });

export const getFacultyCurrentClass = () =>
  api.get<FacultyCurrentClassWorkspaceDto>("/faculty/workspace/current-class");

export const getFacultyTimetable = (params?: { view?: string; anchor?: string }) =>
  api.get<FacultyTimetableViewDto>("/faculty/workspace/timetable", { params });

export const getFacultyInsights = () => api.get<FacultyInsightsDto>("/faculty/workspace/insights");

export const getFacultyNotifications = () =>
  api.get<FacultyScheduleNotificationDto[]>("/faculty/workspace/notifications");

export const getFacultyTimeline = (params?: { date?: string }) =>
  api.get<FacultyTimelineDto>("/faculty/workspace/timeline", { params });

export const getClassroomNavigation = (roomId: number, params?: { fromRoomId?: number }) =>
  api.get<ClassroomNavigationDto>(`/faculty/workspace/rooms/${roomId}/navigation`, { params });

export const getWorkspacePreferences = () => api.get<WorkspacePreferenceDto>("/faculty/workspace/preferences");

export const updateWorkspacePreferences = (payload: {
  landingPage?: string;
  dashboardLayout?: string;
  defaultTimetableView?: string;
  favoriteQuickActions?: string[];
  themePreference?: string;
  notificationPreferences?: Record<string, boolean>;
  oneHandedMode?: boolean;
  highContrast?: boolean;
}) => api.put<WorkspacePreferenceDto>("/faculty/workspace/preferences", payload);

export const getFacultyProductivity = () =>
  api.get<FacultyAttendanceProductivityDto>("/faculty/workspace/productivity");

export const getFacultyProductivityDashboard = () =>
  api.get<FacultyProductivityDashboardDto>("/faculty/workspace/productivity/dashboard");

export const searchFacultyWorkspace = (q: string) =>
  api.get<FacultySearchResponseDto>("/faculty/workspace/search", { params: { q } });

export const getSmartFacultyNotifications = () =>
  api.get<FacultySmartNotificationsDto>("/faculty/workspace/notifications/smart");

export const downloadFacultyCalendarIcs = (params?: { from?: string; to?: string }) =>
  api.get<Blob>("/faculty/workspace/calendar/ics", { params, responseType: "blob" });

export const facultyCalendarSubscribeUrl = (apiBase?: string) => {
  const base = (apiBase ?? (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "").replace(/\/$/, "");
  return `${base}/faculty/workspace/calendar/subscribe.ics`;
};
