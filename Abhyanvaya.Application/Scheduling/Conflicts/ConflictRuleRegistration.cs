using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;
using Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.Providers;
using Abhyanvaya.Application.Scheduling.Conflicts.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace Abhyanvaya.Application.Scheduling.Conflicts;

public static class ConflictRuleRegistration
{
    public static IServiceCollection AddConflictDetection(this IServiceCollection services)
    {
        // Faculty rules
        services.AddScoped<IConflictRule, FacultyDoubleBookingRule>();
        services.AddScoped<IConflictRule, FacultyAvailabilityRule>();
        services.AddScoped<IConflictRule, FacultyPreferenceRule>();
        services.AddScoped<IConflictRule, FacultyMaximumContinuousClassesRule>();
        services.AddScoped<IConflictRule, FacultyBreakViolationRule>();
        services.AddScoped<IConflictRule, FacultyCrossCampusTravelRule>();
        services.AddScoped<IConflictRule, FacultyLunchViolationRule>();
        services.AddScoped<IConflictRule, FacultyWorkingDayViolationRule>();

        // Room rules
        services.AddScoped<IConflictRule, RoomDoubleBookingRule>();
        services.AddScoped<IConflictRule, RoomCapacityExceededRule>();
        services.AddScoped<IConflictRule, RoomWrongFeatureRule>();
        services.AddScoped<IConflictRule, RoomWrongTypeRule>();
        services.AddScoped<IConflictRule, RoomUnavailableRule>();
        services.AddScoped<IConflictRule, RoomMaintenanceConflictRule>();
        services.AddScoped<IConflictRule, RoomLabRequirementRule>();

        // Student rules
        services.AddScoped<IConflictRule, StudentGroupOverlapRule>();
        services.AddScoped<IConflictRule, StudentSemesterOverlapRule>();
        services.AddScoped<IConflictRule, StudentDuplicateSubjectRule>();
        services.AddScoped<IConflictRule, StudentElectiveOverlapRule>();
        services.AddScoped<IConflictRule, StudentBatchConflictRule>();
        services.AddScoped<IConflictRule, StudentPracticalConflictRule>();
        services.AddScoped<IConflictRule, StudentTutorialConflictRule>();

        // Calendar rules
        services.AddScoped<IConflictRule, CalendarHolidayRule>();
        services.AddScoped<IConflictRule, CalendarWorkingDayRule>();
        services.AddScoped<IConflictRule, CalendarSemesterRule>();
        services.AddScoped<IConflictRule, CalendarAcademicYearRule>();
        services.AddScoped<IConflictRule, CalendarClosedCampusRule>();
        services.AddScoped<IConflictRule, CalendarHolidayTypeRule>();

        services.AddScoped<ConflictEngine>();
        services.AddScoped<ConflictAnalyzer>();
        services.AddScoped<IConflictDetectionService, ConflictDetectionService>();
        services.AddScoped<IAttendanceSessionResolver, AttendanceSessionResolver>();

        // AI30 Phase 2B.5 — decision support only (no optimizer / no auto-fix)
        services.AddScoped<IConflictRecommendationProvider, RoomSwapRecommendationProvider>();
        services.AddScoped<IConflictRecommendationProvider, FacultySwapRecommendationProvider>();
        services.AddScoped<IConflictRecommendationProvider, TimeSlotRecommendationProvider>();
        services.AddScoped<IConflictResolutionAdvisor, ConflictResolutionAdvisor>();
        services.AddScoped<IImpactAnalyzer, ImpactAnalyzer>();
        services.AddScoped<IConflictDependencyAnalyzer, ConflictDependencyAnalyzer>();
        services.AddScoped<IConflictExplainabilityService, ConflictExplainabilityService>();
        services.AddScoped<IConflictRuleConfigurationService, ConflictRuleConfigurationService>();
        services.AddScoped<IConflictAnalyticsService, ConflictAnalyticsService>();
        services.AddScoped<IConflictIntelligenceService, ConflictIntelligenceService>();
        return services;
    }
}
