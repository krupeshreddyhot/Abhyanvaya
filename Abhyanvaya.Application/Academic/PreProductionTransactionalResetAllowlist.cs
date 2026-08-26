namespace Abhyanvaya.Application.Academic;

/// <summary>
/// Explicit deletion allowlist / protected denylist for Prompt 3HC1.
/// Classifications are based on EF FK discovery — not table-name inference alone.
/// </summary>
public static class PreProductionTransactionalResetAllowlist
{
    public const string Delete = "DELETE";
    public const string Preserve = "PRESERVE";

    /// <summary>Dependency-safe deletion order (dependents before parents).</summary>
    public static readonly string[] DeletionOrder =
    [
        // Attendance graph
        "AttendanceRecognitionReviewHistory",
        "AttendanceDetail",
        "Attendance",
        "AttendanceRecognition",
        "AttendanceSessionImage",
        "AttendanceRetryHistory",
        "AttendanceSessionSection",
        "AttendanceSession",
        "ClassSchedule",
        "AttendanceBulkOperationHistory",
        // Conflict / workspace
        "ConflictFinding",
        "ConflictWorkspacePin",
        "ConflictWorkspaceBookmark",
        "ConflictWorkspaceNote",
        "ConflictDetectionRun",
        // Timetable governance
        "TimetableApprovalHistory",
        "TimetableApprovalComment",
        "TimetableDecisionHistory",
        "TimetableApprovalStep",
        "TimetableApprovalRequest",
        "TimetableChangeHistory",
        "TimetableWarningDismissal",
        "TimetableCloneJob",
        "TimetableSection",
        "TimetableEntry",
        "Timetable",
        "ScheduleVersion",
        // Optimization sandbox
        "OptimizationScenarioComment",
        "OptimizationScenarioNote",
        "OptimizationScenarioFavorite",
        "OptimizationScenarioBookmark",
        "OptimizationScenarioApprovalRequest",
        "OptimizationScenarioShare",
        "OptimizationScenarioHistory",
        "OptimizationMetricSnapshot",
        "OptimizationTelemetryAggregate",
        "OptimizationSnapshot",
        "OptimizationSimulationRun",
        "OptimizationEngineRun",
        "OptimizationScenario",
        // Teaching groups (after TimetableEntry clears Restrict TG refs)
        "TeachingGroupMembership",
        "TeachingGroupSection",
        "TeachingGroup",
        // Scheduling allocations (after TG)
        "SubjectAllocation",
    ];

    public static readonly string[] ProtectedEntities =
    [
        "Student",
        "Course",
        "Group",
        "Semester",
        "Department",
        "Program",
        "College",
        "University",
        "Subject",
        "TenantSubject",
        "ElectiveGroup",
        "Section",
        "StudentSection",
        "User",
        "Permission",
        "ApplicationRole",
        "ApplicationRolePermission",
        "UserApplicationRole",
        "TenantAcademicConfiguration",
        "ProgramPolicy",
        "Staff",
        "AcademicYear",
        "AcademicTerm",
        "WorkingDay",
        "Holiday",
        "Campus",
        "Building",
        "Floor",
        "Room",
        "TimeSlotSet",
        "TimeSlot",
        "ConflictRuleThresholdSetting",
        "AttendanceRecoveryPreference",
        "WorkspacePreference",
        "LegacySemesterDispositionJournal",
        "StudentFaceEmbedding",
        "FacultyWorkload",
        "FacultyDayPreference",
        "FacultyTimeSlotPreference",
        "FacultyTeachingPreference",
        "FacultyAvailability",
        "RoomAllocationRule",
        "RoomAvailability",
    ];
}
