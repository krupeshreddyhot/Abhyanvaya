using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Allocation;
using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Dashboards;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Capacity;
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
        services.AddScoped<ISectionCapacityEngine, SectionCapacityEngine>();
        services.AddScoped<ISectionManagementService, SectionManagementService>();

        // AI29.1B — Section lifecycle, capacity ops, merge/split, readiness, reports
        services.AddScoped<ISectionLifecycleService, SectionLifecycleService>();
        services.AddScoped<ISectionMergeService, SectionMergeService>();
        services.AddScoped<ISectionSplitService, SectionSplitService>();
        services.AddScoped<ISectionReadinessService, SectionReadinessService>();
        services.AddScoped<ISectionGroupService, SectionGroupService>();
        services.AddScoped<ISectionOperationalReportService, SectionOperationalReportService>();
        services.AddScoped<ISectionAllocationRecommendationService, NullSectionAllocationRecommendationService>();

        // AI29.1B.5 — Section operations hardening
        services.AddScoped<ISectionVersioningService, SectionVersioningService>();
        services.AddScoped<ISectionCapacityHistoryService, SectionCapacityHistoryService>();
        services.AddScoped<ISectionTimelineService, SectionTimelineService>();
        services.AddScoped<IMergePreviewService, MergePreviewService>();
        services.AddScoped<ISplitPreviewService, SplitPreviewService>();
        services.AddScoped<ISectionPolicyService, SectionPolicyService>();
        services.AddScoped<ISectionCapacityRecommendationService, SectionCapacityRecommendationService>();
        services.AddScoped<ISectionHealthService, SectionHealthService>();

        // AI29.1B.7 — Allocation platform & readiness
        services.AddScoped<ISectionAllocationContextValidator, SectionAllocationContextValidator>();
        services.AddScoped<IAllocationSnapshotService, AllocationSnapshotService>();
        services.AddScoped<ISectionAllocationContextBuilder, SectionAllocationContextBuilder>();
        services.AddScoped<IAllocationReadinessService, AllocationReadinessService>();
        services.AddScoped<IAllocationHealthService, AllocationHealthService>();
        services.AddScoped<IAllocationContextCache, AllocationContextCache>();

        // AI29.1C — Enterprise Section Allocation Engine
        services.AddScoped<IStudentGroupingStrategy, StudentGroupingStrategy>();
        services.AddScoped<IAllocationScoreCalculator, AllocationScoreCalculator>();
        services.AddScoped<IAllocationScoringProvider, AllocationScoreCalculator>();
        services.AddScoped<IAllocationConstraintEngine, AllocationConstraintEngine>();
        services.AddScoped<IAllocationRecommendationProvider, ContextAllocationRecommendationProvider>();
        services.AddScoped<IAllocationPipelineStrategy, ValidationAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, RollNumberBandsAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, CapacityAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, PolicyAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, GenderAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, LanguageAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, ScholarshipAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, ElectiveAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, TransportAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, HostelAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, MeritAllocationStrategy>();
        services.AddScoped<IAllocationPipelineStrategy, ScoringAllocationStrategy>();
        services.AddScoped<IAllocationConstraint, CapacityAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, ReservedSeatsAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, GenderBalanceAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, LanguageAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, HostelAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, MeritAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, ElectiveAllocationConstraint>();
        services.AddScoped<IAllocationConstraint, MinorSubjectAllocationConstraint>();
        services.AddScoped<IAllocationEngine, AllocationEngine>();
        services.AddScoped<IAllocationExecutionService, AllocationExecutionService>();
        services.AddScoped<IAllocationSimulationService, AllocationSimulationService>();
        services.AddScoped<IAllocationApprovalService, AllocationApprovalService>();
        services.AddScoped<IAllocationSandboxService, AllocationSandboxService>();
        services.AddScoped<IAllocationDashboardService, AllocationDashboardService>();
        services.AddScoped<IAllocationReportService, AllocationReportService>();
        // IAllocationProgressPublisher registered by API host (SignalR) or tests (Null)

        // AI29.1C.5 — Allocation intelligence & enterprise operations
        services.AddScoped<IAllocationAuditService, AllocationAuditService>();
        services.AddScoped<IAllocationScenarioVersioningService, AllocationScenarioVersioningService>();
        services.AddScoped<IAllocationScenarioVersionService>(sp => sp.GetRequiredService<IAllocationScenarioVersioningService>());
        services.AddScoped<IAllocationScenarioLifecycleService, AllocationScenarioLifecycleService>();
        services.AddScoped<IAllocationHistoryService, AllocationHistoryService>();
        services.AddScoped<IAllocationReplayService, AllocationReplayService>();
        services.AddScoped<IAllocationComparisonService, AllocationComparisonService>();
        services.AddScoped<IAllocationExplanationService, AllocationExplanationService>();
        services.AddScoped<IAllocationAnalyticsService, AllocationAnalyticsService>();
        services.AddScoped<IAllocationGovernanceService, AllocationGovernanceService>();
        services.AddScoped<IAllocationOpsDashboardService, AllocationOpsDashboardService>();
        services.AddScoped<IAllocationScenarioQueryService, AllocationScenarioQueryService>();

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
        services.AddScoped<ILegacySemesterMigrationAuditService, LegacySemesterMigrationAuditService>();
        services.AddScoped<ILegacySemesterMigrationDecisionPlanService, LegacySemesterMigrationDecisionPlanService>();
        services.AddScoped<ISemesterIiiSplitStudentRemapMigrationService, SemesterIiiSplitStudentRemapMigrationService>();
        services.AddScoped<ISemesterPostMigrationIntegrityAuditService, SemesterPostMigrationIntegrityAuditService>();
        services.AddScoped<ILegacySemesterDownstreamRemediationService, LegacySemesterDownstreamRemediationService>();
        services.AddScoped<ILegacySemesterFinalizationAuditService, LegacySemesterFinalizationAuditService>();
        services.AddScoped<ILegacySemesterFinalizationExecutionService, LegacySemesterFinalizationExecutionService>();
        services.AddScoped<ILegacySemesterWildcardRetirementService, LegacySemesterWildcardRetirementService>();
        services.AddScoped<ISemesterSchemaHardeningReadinessService, SemesterSchemaHardeningReadinessService>();
        services.AddScoped<ILegacySemesterFinalDispositionReadinessService, LegacySemesterFinalDispositionReadinessService>();
        services.AddScoped<ILegacySemesterHistoricalDispositionService, LegacySemesterHistoricalDispositionService>();
        services.AddScoped<IHistoricalSemesterDispositionAuditService, HistoricalSemesterDispositionAuditService>();
        services.AddScoped<IHistoricalSemesterDispositionExecutionService, HistoricalSemesterDispositionExecutionService>();
        services.AddScoped<IPreProductionTransactionalResetService, PreProductionTransactionalResetService>();
        services.AddScoped<ITeachingGroupSemesterRemediationService, TeachingGroupSemesterRemediationService>();
        services.AddScoped<ITeachingGroupRemediationReadinessService, TeachingGroupRemediationReadinessService>();
        services.AddScoped<ISectionSemesterRemediationService, SectionSemesterRemediationService>();
        services.AddScoped<IFinanceSectionSemesterRemediationService, FinanceSectionSemesterRemediationService>();
        services.AddScoped<ISectionSemesterRemediationAuditService, SectionSemesterRemediationAuditService>();
        services.AddScoped<ISubjectCatalogSemesterRemediationService, SubjectCatalogSemesterRemediationService>();
        services.AddScoped<IPrompt3HPostSectionIntegrityAuditService, Prompt3HPostSectionIntegrityAuditService>();
        services.AddScoped<ICourseMasterWriteService, CourseMasterWriteService>();
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
        services.AddScoped<ITeachingGroupApplicationService, TeachingGroupApplicationService>();
        services.AddScoped<ICompatibleTeachingGroupQueryService, CompatibleTeachingGroupQueryService>();
        services.AddScoped<ITeachingGroupManagementApplicationService, TeachingGroupManagementApplicationService>();
        services.AddScoped<ITeachingGroupMembershipResolver, TeachingGroupMembershipResolver>();
        services.AddScoped<ITeachingGroupMembershipApplicationService, TeachingGroupMembershipApplicationService>();
        services.AddScoped<ITeachingGroupSectionApplicationService, TeachingGroupSectionApplicationService>();
        services.AddScoped<ITimetableSectionProjector, TimetableSectionProjector>();
        // AI-SCHED-TG.4A Prompt 7 — explicit disposable conversion only (not a hosted/startup job).
        services.AddScoped<ILegacyTimetableTeachingGroupConversionService, LegacyTimetableTeachingGroupConversionService>();
        // AI-SCHED-CAP Prompt 3 — shared PlacementSize (room-fit); TG capacity remains separate.
        services.AddSingleton<IPlacementSizeResolver, PlacementSizeResolver>();
        // AI-SCHED-CAP Prompt 3A — shared room-capacity (margin-aware) for ConflictEngine + SoftValidation.
        services.AddSingleton<IRoomCapacityEvaluator, RoomCapacityEvaluator>();
        // AI-SCHED-CAP Prompt 4 — presentation/classification for actionable soft feedback.
        services.AddSingleton<ISchedulingConflictPresentationComposer, SchedulingConflictPresentationComposer>();
        // AI-SCHED-CAP Prompt 6/7 — publish readiness (read-only evaluate + PublishAsync gate consumer).
        services.AddScoped<ITimetablePublishReadinessService, TimetablePublishReadinessService>();
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
