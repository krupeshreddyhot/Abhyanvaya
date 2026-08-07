using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Configuration;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.Scheduling.Optimization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IStudentService, StudentService>();

        // AI29 — Academic Structure & Section Management
        services.AddScoped<ISectionManagementService, SectionManagementService>();

        // AI29.1A / AI29.1A.5 / AI29.1A.6 / AI29.1A.7 — Academic Hierarchy + Observability
        services.AddOptions<AcademicHierarchyOptions>()
            .BindConfiguration(AcademicHierarchyOptions.SectionName);
        services.AddOptions<AcademicPlatformOptions>()
            .BindConfiguration(AcademicPlatformOptions.SectionName);
        services.AddSingleton<AcademicMetricsStore>();
        services.AddScoped<IAcademicTelemetryService, AcademicTelemetryService>();
        services.AddScoped<IAcademicCacheMetricsService, AcademicCacheMetricsService>();
        services.AddScoped<IAcademicPerformanceMonitor, AcademicPerformanceMonitor>();
        services.AddScoped<IAcademicDomainEventMetrics, AcademicDomainEventMetrics>();
        services.AddScoped<IAcademicHealthService, AcademicHealthService>();
        services.AddScoped<IAcademicArchitectureTrendService, AcademicArchitectureTrendService>();
        services.AddScoped<IAcademicPlatformMetricsService, AcademicPlatformMetricsService>();
        services.AddScoped<IAcademicHierarchyCache, AcademicHierarchyCache>();
        services.AddScoped<IAcademicStatisticsCache, AcademicStatisticsCache>();
        services.AddScoped<IAcademicTreeService, AcademicTreeService>();
        services.AddScoped<IAcademicBreadcrumbService, AcademicBreadcrumbService>();
        services.AddScoped<IAcademicSearchService, AcademicSearchService>();
        services.AddScoped<IAcademicHierarchySnapshotService, AcademicHierarchySnapshotService>();
        services.AddScoped<IAcademicCatalogService, AcademicCatalogService>();
        services.AddScoped<IAcademicHierarchyService, AcademicHierarchyService>();
        services.AddScoped<IAcademicStructureService, AcademicStructureService>();
        services.AddScoped<IAttendanceSessionQueryService, AttendanceSessionQueryService>();
        services.AddScoped<IAttendanceRecognitionReviewService, AttendanceRecognitionReviewService>();
        services.AddScoped<IAttendanceSessionSummaryService, AttendanceSessionSummaryService>();
        services.AddScoped<IAttendanceSessionAnalyticsService, AttendanceSessionAnalyticsService>();
        services.AddScoped<IAttendanceBuilder, AttendanceBuilder>();
        services.AddScoped<IAttendanceSessionFinalizer, AttendanceSessionFinalizer>();
        services.AddScoped<IStudentFaceEmbeddingService, StudentFaceEmbeddingService>();
        services.AddScoped<IAttendanceSessionCreator, AttendanceSessionCreator>();
        services.AddScoped<IAttendancePhotoService, AttendancePhotoService>();
        services.AddScoped<IClassroomPhotoService, AttendancePhotoService>();
        services.AddScoped<IClassScheduleService, ClassScheduleService>();

        services.AddScoped<IAcademicCalendarService, AcademicCalendarService>();
        services.AddScoped<ICampusFacilityService, CampusFacilityService>();
        services.AddScoped<ITimeSlotService, TimeSlotService>();
        services.AddScoped<IFacultyWorkloadService, FacultyWorkloadService>();
        services.AddScoped<ISubjectAllocationService, SubjectAllocationService>();
        services.AddScoped<IRoomAllocationRuleService, RoomAllocationRuleService>();
        services.AddScoped<ISchedulingDashboardService, SchedulingDashboardService>();
        services.AddScoped<IFacultyAvailabilityService, FacultyAvailabilityService>();
        services.AddScoped<IRoomAvailabilityService, RoomAvailabilityService>();
        services.AddScoped<ISubjectCategoryService, SubjectCategoryService>();
        services.AddScoped<ITimeSlotTemplateService, TimeSlotTemplateService>();
        services.AddScoped<IFacultyTeachingPreferenceService, FacultyTeachingPreferenceService>();
        services.AddScoped<IRoomFeatureService, RoomFeatureService>();
        services.AddScoped<ISubjectDeliveryTypeService, SubjectDeliveryTypeService>();
        services.AddScoped<IHolidayTypeCatalogService, HolidayTypeCatalogService>();
        services.AddScoped<ISchedulingValidationService, SchedulingValidationService>();
        // AI30 Phase 3.5 — guided configuration experience (read-only readiness / validation)
        services.AddScoped<ISchedulingConfigurationReadinessService, SchedulingConfigurationReadinessService>();
        services.AddScoped<ISchedulingSetupValidator, SchedulingSetupValidator>();
        services.AddScoped<ITimetableService, TimetableService>();
        services.AddScoped<ITimetableExportService, TimetableExportService>();
        services.AddScoped<IScheduleVersionService, ScheduleVersionService>();
        services.AddScoped<ITimetableApprovalService, TimetableApprovalService>();
        services.AddScoped<ITimetableLifecycleService, TimetableLifecycleService>();
        services.AddScoped<ITimetableCloneService, TimetableCloneService>();
        services.AddScoped<ITimetableSoftValidationService, TimetableSoftValidationService>();
        services.AddScoped<ITimetableChangeHistoryService, TimetableChangeHistoryService>();
        services.AddScoped<ITimetableGovernanceDashboardService, TimetableGovernanceDashboardService>();
        services.AddScoped<IVersionComparisonService, VersionComparisonService>();
        services.AddConflictDetection();
        services.AddOptimizationReadiness();

        // AI31 — Intelligent Faculty Experience Platform (aggregates existing services)
        services.AddScoped<IFacultyDashboardService, FacultyDashboardService>();
        services.AddScoped<IFacultyScheduleNotifier, NoOpFacultyScheduleNotifier>();

        // AI31.5 — Faculty workspace enhancements (composition only)
        services.AddScoped<IWorkspacePreferenceService, WorkspacePreferenceService>();
        services.AddScoped<IFacultyCalendarService, FacultyCalendarService>();
        services.AddScoped<IFacultyTimelineService, FacultyTimelineService>();
        services.AddScoped<IClassroomNavigationService, ClassroomNavigationService>();
        services.AddScoped<IFacultyProductivityService, FacultyProductivityService>();
        services.AddScoped<IFacultySearchService, FacultySearchService>();
        services.AddScoped<IFacultySmartNotificationService, FacultySmartNotificationService>();

        // AI22.8 / AI22.8.5 — Enterprise Attendance Recovery (composes existing session/recognition pipeline)
        services.AddScoped<IAttendanceWorkflowLifecycleService, AttendanceWorkflowLifecycleService>();
        services.AddScoped<IPendingAttendanceService, PendingAttendanceService>();
        services.AddScoped<IPendingSessionQueueService, PendingSessionQueueService>();
        services.AddScoped<IAttendanceResumeService, AttendanceResumeService>();
        services.AddScoped<IAttendanceRetryService, AttendanceRetryService>();
        services.AddScoped<IAttendanceRecoverySearchService, AttendanceRecoverySearchService>();
        services.AddScoped<IAttendanceRecoveryDashboardService, AttendanceRecoveryDashboardService>();
        services.AddScoped<IAttendanceExpirationService, AttendanceExpirationService>();
        services.AddScoped<IAttendanceRecoveryPreferenceService, AttendanceRecoveryPreferenceService>();
        services.AddScoped<IFacultyRecoveryCenterService, FacultyRecoveryCenterService>();
        services.AddScoped<IAttendanceOperationsDashboardService, AttendanceOperationsDashboardService>();
        services.AddScoped<IAttendanceOperationalAnalyticsService, AttendanceOperationalAnalyticsService>();
        services.AddScoped<IAttendanceHealthMonitorService, AttendanceHealthMonitorService>();
        services.AddScoped<IFacultyWorkspaceRecoverySummaryService, FacultyWorkspaceRecoverySummaryService>();
        services.AddScoped<IDepartmentOperationsService, DepartmentOperationsService>();
        services.AddScoped<ISessionTimelineService, SessionTimelineService>();
        services.AddScoped<IBulkOperationService, BulkOperationService>();
        services.AddScoped<IEnterpriseOpsDashboardService, EnterpriseOpsDashboardService>();
        services.AddScoped<IAttendanceRecoveryNotifier, NoOpAttendanceRecoveryNotifier>();

        // AI31.6 — Enterprise Dashboards & Operational Intelligence (composition only)
        services.AddScoped<IDashboardPreferenceService, DashboardPreferenceService>();
        services.AddScoped<IFacultyCommandCenterService, FacultyCommandCenterService>();
        services.AddScoped<IAdminOperationsDashboardService, AdminOperationsDashboardService>();
        services.AddScoped<IEnterpriseOperationalAnalyticsComposer, EnterpriseOperationalAnalyticsComposer>();
        services.AddScoped<IEnterpriseHealthCenterService, EnterpriseHealthCenterService>();
        services.AddScoped<IEnterpriseNotificationCenterService, EnterpriseNotificationCenterService>();

        // AI31.7 — Enterprise Operations Command Center (composition only)
        services.AddScoped<IOperationsCommandCenterService, OperationsCommandCenterService>();

        // AI31.8 — Enterprise Operations Dashboard Excellence (UX / composition only)
        services.AddScoped<IEnterpriseDashboardExcellenceService, EnterpriseDashboardExcellenceService>();

        return services;
    }
}
