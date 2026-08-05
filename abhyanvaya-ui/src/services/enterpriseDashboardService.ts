import api from "../api/axios";

export type DashboardWidgetDto = {
  code: string;
  title: string;
  kind: string;
  category: string;
  value?: number | null;
  displayValue?: string | null;
  unit?: string | null;
  status?: string | null;
  statusLabel?: string | null;
  path?: string | null;
  reportPath?: string | null;
  tooltip?: string | null;
  lastUpdatedUtc?: string | null;
  trend?: string | null;
  comparison?: string | null;
  suggestedAction?: string | null;
  estimatedImpact?: string | null;
  actionLabel?: string | null;
  group?: string | null;
  explanation?: string | null;
  pinned?: boolean;
  requiredPermission?: string | null;
  configurable: boolean;
  visible: boolean;
  sortOrder: number;
};

export type CommandCenterSectionDto = {
  code: string;
  title: string;
  icon?: string | null;
  subtitle?: string | null;
  collapsedByDefault?: boolean;
  cards: DashboardWidgetDto[];
  groupOrder?: string[];
  quickLinks: { label: string; path: string }[];
};

export type CommandCenterQuickActionDto = {
  code: string;
  label: string;
  path: string;
  shortcut?: string | null;
  requiredPermission?: string | null;
  primary: boolean;
};

export type CommandCenterActionBannerDto = {
  code: string;
  message: string;
  path: string;
  actionLabel: string;
  severity: string;
  requiredPermission?: string | null;
};

export type EnterpriseOperationsCommandCenterDto = {
  title: string;
  subtitle?: string;
  refreshIntervalSeconds?: number;
  attentionRequired: CommandCenterSectionDto;
  todaysOperations: CommandCenterSectionDto;
  schedulingOperations: CommandCenterSectionDto;
  attendanceOperations: CommandCenterSectionDto;
  academicResources: CommandCenterSectionDto;
  systemHealth: CommandCenterSectionDto;
  actionBanners?: CommandCenterActionBannerDto[];
  quickActions: CommandCenterQuickActionDto[];
  preferences: DashboardPreferenceDto;
  generatedUtc: string;
};

export type FacultyCommandCenterDto = {
  date: string;
  mode: string;
  hasTimetable: boolean;
  message: string;
  currentClass?: FacultyCommandClassCardDto | null;
  nextClass?: FacultyCommandClassCardDto | null;
  todaysClasses: FacultyCommandClassCardDto[];
  remainingClasses: number;
  todaysStudents: number;
  attendancePending: number;
  recoveryQueue: number;
  kpis: FacultyKpiBundleDto;
  insights: FacultyInsightsPanelDto;
  activityPreview: FacultyActivityEventDto[];
  widgets: DashboardWidgetDto[];
  quickActions: FacultyCommandQuickActionDto[];
  preferences: DashboardPreferenceDto;
  generatedUtc: string;
};

export type FacultyCommandClassCardDto = {
  status: string;
  subjectName?: string | null;
  roomName?: string | null;
  startTime?: string | null;
  endTime?: string | null;
  attendanceStatus: string;
  studentCount?: number | null;
  attendanceSessionId?: string | null;
  takeAttendancePath?: string | null;
};

export type FacultyCommandQuickActionDto = {
  code: string;
  label: string;
  path: string;
  primary: boolean;
};

export type FacultyKpiBundleDto = {
  todaysClasses: number;
  completedClasses: number;
  remainingClasses: number;
  todaysStudents: number;
  attendanceCompleted: number;
  pendingAttendance: number;
  recoverySessions: number;
  recognitionReviews: number;
  averageCompletionMinutes?: number | null;
  attendancePercent?: number | null;
};

export type InsightItemDto = {
  code: string;
  kind: string;
  title: string;
  message: string;
  path?: string | null;
  severity: string;
};

export type FacultyInsightsPanelDto = {
  items: InsightItemDto[];
};

export type FacultyActivityEventDto = {
  eventId: string;
  kind: string;
  title: string;
  message: string;
  occurredUtc: string;
  path?: string | null;
};

export type FacultyActivityTimelineDto = {
  range: string;
  events: FacultyActivityEventDto[];
};

export type AdminSectionDto = {
  code: string;
  title: string;
  cards: DashboardWidgetDto[];
  quickLinks: { label: string; path: string }[];
};

export type AdminOperationsDashboardDto = {
  academic: AdminSectionDto;
  attendance: AdminSectionDto;
  scheduling: AdminSectionDto;
  faculty: AdminSectionDto;
  student: AdminSectionDto;
  recovery: AdminSectionDto;
  aiServices: AdminSectionDto;
  platformHealth: AdminSectionDto;
  widgets: DashboardWidgetDto[];
  charts: OperationalChartSeriesDto[];
  preferences: DashboardPreferenceDto;
  generatedUtc: string;
};

export type OperationalChartSeriesDto = {
  code: string;
  title: string;
  points: { label: string; value: number }[];
};

export type EnterpriseOperationalAnalyticsDto = {
  series: OperationalChartSeriesDto[];
  departmentComparison: {
    departmentName: string;
    pendingSessions: number;
    completed: number;
    averageCompletionMinutes?: number | null;
  }[];
  generatedUtc: string;
};

export type EnterpriseNotificationItemDto = {
  notificationId: string;
  source: string;
  category: string;
  priority: string;
  title: string;
  message: string;
  occurredUtc: string;
  isUnread: boolean;
  isPinned: boolean;
  isDismissed: boolean;
  isArchived: boolean;
  path?: string | null;
};

export type EnterpriseNotificationCenterDto = {
  items: EnterpriseNotificationItemDto[];
  unreadCount: number;
};

export type DashboardFilterRequest = {
  academicYearId?: number | null;
  departmentId?: number | null;
  courseId?: number | null;
  campusId?: number | null;
  buildingId?: number | null;
  roomId?: number | null;
};

export type NamedOptionDto = { id: number; name: string };

export type DashboardFilterStateDto = DashboardFilterRequest & {
  academicYears: NamedOptionDto[];
  departments: NamedOptionDto[];
  courses: NamedOptionDto[];
  campuses: NamedOptionDto[];
  buildings: NamedOptionDto[];
  rooms: NamedOptionDto[];
};

export type ExecutiveSummaryDto = {
  academicYear?: string | null;
  collegeName?: string | null;
  currentSemester?: string | null;
  todaysDate: string;
  currentWorkingDay: string;
  totalScheduledClassesToday?: number | null;
  overallAttendanceToday?: string | null;
  facultyAvailableToday?: number | null;
  activeStudents?: number | null;
  criticalAlerts: number;
  platformHealth: string;
  platformHealthStatus?: string | null;
  cards: DashboardWidgetDto[];
};

export type AcademicTimelineItemDto = {
  kind: string;
  label: string;
  status: string;
  startTime?: string | null;
  endTime?: string | null;
  facultyOccupancy?: number | null;
  roomOccupancy?: number | null;
  isCurrent: boolean;
};

export type AcademicTimelineDto = {
  currentPeriodLabel?: string | null;
  currentTime: string;
  items: AcademicTimelineItemDto[];
};

export type DashboardVisualizationsDto = {
  attendanceHeatmap?: OperationalChartSeriesDto | null;
  departmentHeatmap?: OperationalChartSeriesDto | null;
  facultyWorkloadHeatmap?: OperationalChartSeriesDto | null;
  roomUtilizationHeatmap?: OperationalChartSeriesDto | null;
  weeklyAttendanceTrend?: OperationalChartSeriesDto | null;
  schedulingCompletion?: OperationalChartSeriesDto | null;
  conflictTrend?: OperationalChartSeriesDto | null;
};

export type WidgetHelpDto = {
  widgetCode: string;
  purpose: string;
  howCalculated: string;
  updateFrequency: string;
  relatedModules: string[];
  navigationLinks: { label: string; path: string }[];
};

export type ActionGroupDto = {
  code: string;
  title: string;
  actions: CommandCenterQuickActionDto[];
};

export type EnterpriseDashboardExcellenceDto = {
  title: string;
  executiveSummary: ExecutiveSummaryDto;
  filters: DashboardFilterStateDto;
  commandCenter: EnterpriseOperationsCommandCenterDto;
  academicTimeline: AcademicTimelineDto;
  visualizations: DashboardVisualizationsDto;
  widgetHelp: WidgetHelpDto[];
  actionGroups: ActionGroupDto[];
  preferences: DashboardPreferenceDto;
  refreshIntervalSeconds: number;
  generatedUtc: string;
  nextRefreshUtc?: string | null;
};

export type DashboardPreferenceDto = {
  id: number;
  roleScope: string;
  defaultLandingPage: string;
  compactMode: boolean;
  hiddenWidgets: string[];
  widgetOrder: string[];
  pinnedWidgets?: string[];
  filters?: DashboardFilterRequest | null;
  refreshIntervalSeconds?: number;
  highContrast?: boolean;
};

export type EnterpriseHealthCenterDto = {
  overallStatus: string;
  components: { code: string; title: string; status: string; message: string }[];
  generatedUtc: string;
};

const base = "/enterprise-dashboards";

export const getFacultyCommandCenter = () => api.get<FacultyCommandCenterDto>(`${base}/faculty/command-center`);
export const getFacultyKpis = () => api.get<FacultyKpiBundleDto>(`${base}/faculty/kpis`);
export const getFacultyInsightsPanel = () => api.get<FacultyInsightsPanelDto>(`${base}/faculty/insights`);
export const getFacultyActivityTimeline = (range = "Today") =>
  api.get<FacultyActivityTimelineDto>(`${base}/faculty/activity-timeline`, { params: { range } });
export const getAdminOperationsDashboard = () => api.get<AdminOperationsDashboardDto>(`${base}/admin/operations`);
export const getAdminCommandCenter = (filters?: DashboardFilterRequest) =>
  api.get<EnterpriseOperationsCommandCenterDto>(`${base}/admin/command-center`, { params: filters });
export const getAdminDashboardExcellence = (filters?: DashboardFilterRequest) =>
  api.get<EnterpriseDashboardExcellenceDto>(`${base}/admin/excellence`, { params: filters });
export const exportAdminDashboardExcellence = (body: {
  format: string;
  filters?: DashboardFilterRequest | null;
}) =>
  api.post(`${base}/admin/excellence/export`, body, { responseType: "blob" });
export const getEnterpriseAnalytics = () => api.get<EnterpriseOperationalAnalyticsDto>(`${base}/analytics`);
export const getEnterpriseHealth = () => api.get<EnterpriseHealthCenterDto>(`${base}/health`);
export const getEnterpriseNotifications = () => api.get<EnterpriseNotificationCenterDto>(`${base}/notifications`);
export const updateNotificationState = (body: {
  notificationId: string;
  isRead?: boolean;
  isPinned?: boolean;
  isDismissed?: boolean;
  isArchived?: boolean;
}) => api.post<EnterpriseNotificationCenterDto>(`${base}/notifications/state`, body);
export const getDashboardPreferences = (roleScope?: string) =>
  api.get<DashboardPreferenceDto>(`${base}/preferences`, { params: { roleScope } });
export const upsertDashboardPreferences = (
  body: Partial<DashboardPreferenceDto> & { roleScope?: string; restoreDefaults?: boolean },
) => api.put<DashboardPreferenceDto>(`${base}/preferences`, body);
