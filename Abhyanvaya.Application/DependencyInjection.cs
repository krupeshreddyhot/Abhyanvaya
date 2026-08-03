using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Application.Scheduling;
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

        return services;
    }
}
