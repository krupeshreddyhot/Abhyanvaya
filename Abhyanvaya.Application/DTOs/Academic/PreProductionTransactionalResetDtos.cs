namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (package 3HC1 / PromptCode P1-4-3HC1) —
/// Pre-production transactional reset &amp; Student Semester reconciliation.
/// </summary>
public static class PreProductionTransactionalResetCodes
{
    public const string PromptCode = "P1-4-3HC1";
    public const string ConfirmationPhrase = "PREPRODUCTION_TRANSACTIONAL_RESET";
    public const string DispositionCode = "PREPRODUCTION_TRANSACTIONAL_RESET";
}

public static class StudentSemesterResolutionStatuses
{
    public const string AlreadyCorrect = "ALREADY_CORRECT";
    public const string UpdateRequired = "UPDATE_REQUIRED";
    public const string Ambiguous = "AMBIGUOUS";
    public const string NoSemester = "NO_SEMESTER";
    public const string InvalidGroup = "INVALID_GROUP";
    public const string CourseGroupMismatch = "COURSE_GROUP_MISMATCH";
}

public sealed class PreProductionTransactionalResetExecuteRequest
{
    /// <summary>Must be true.</summary>
    public bool Confirm { get; set; }

    /// <summary>Must equal <see cref="PreProductionTransactionalResetCodes.ConfirmationPhrase"/>.</summary>
    public string? ConfirmationPhrase { get; set; }

    public string? Reason { get; set; }
}

public sealed class PreProductionEntityCountDto
{
    public string Entity { get; init; } = "";
    public string Classification { get; init; } = "";
    public int Count { get; init; }
    public string RecommendedAction { get; init; } = "";
}

public sealed class StudentSemesterReconciliationRowDto
{
    public int StudentId { get; init; }
    public string StudentNumber { get; init; } = "";
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int CurrentSemesterId { get; init; }
    public int? ResolvedSemesterId { get; init; }
    public string ResolutionStatus { get; init; } = "";
    public string Evidence { get; init; } = "";
}

public sealed class ProtectedCountsDto
{
    public int Students { get; init; }
    public int Courses { get; init; }
    public int Groups { get; init; }
    public int Semesters { get; init; }
    public int Departments { get; init; }
    public int Programs { get; init; }
    public int Colleges { get; init; }
    public int Subjects { get; init; }
    public int Users { get; init; }
    public int Permissions { get; init; }
    public int ApplicationRoles { get; init; }
    public int TenantAcademicConfigurations { get; init; }
}

public sealed class PreProductionTransactionalResetPreviewDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public string PromptCode { get; init; } = PreProductionTransactionalResetCodes.PromptCode;
    public bool IsReadOnly { get; init; } = true;
    public bool SaveChangesInvoked { get; init; }
    public bool IsCleanupReady { get; init; }
    public string? AbortReason { get; init; }
    public ProtectedCountsDto ProtectedBefore { get; init; } = new();
    public IReadOnlyList<PreProductionEntityCountDto> DeletionAllowlistCounts { get; init; } = [];
    public IReadOnlyList<PreProductionEntityCountDto> ProtectedDenylistCounts { get; init; } = [];
    public IReadOnlyList<string> DeletionOrder { get; init; } = [];
    public int TransactionalTotal { get; init; }
    public int StudentsUpdateRequired { get; init; }
    public int StudentsAlreadyCorrect { get; init; }
    public int StudentsFailClosed { get; init; }
    public IReadOnlyList<StudentSemesterReconciliationRowDto> StudentReconciliation { get; init; } = [];
    public IReadOnlyList<string> Blockers { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public bool SchemaHardeningDeferred { get; init; } = true;
}

public sealed class PreProductionTransactionalResetExecuteResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public string PromptCode { get; init; } = PreProductionTransactionalResetCodes.PromptCode;
    public string CorrelationId { get; init; } = "";
    public bool IsSuccessful { get; init; }
    public string ExecutionStatus { get; init; } = "";
    public bool RolledBack { get; init; }
    public bool TransactionCommitted { get; init; }
    public string TransactionModel { get; init; } = "ALL_OR_NOTHING";
    public string? AbortReason { get; init; }
    public ProtectedCountsDto ProtectedBefore { get; init; } = new();
    public ProtectedCountsDto ProtectedAfter { get; init; } = new();
    public IReadOnlyList<PreProductionEntityCountDto> DeletedCounts { get; init; } = [];
    public int TotalDeleted { get; init; }
    public int StudentsUpdated { get; init; }
    public int StudentsAlreadyCorrect { get; init; }
    public IReadOnlyList<StudentSemesterReconciliationRowDto> StudentReconciliation { get; init; } = [];
    public bool IdempotentZeroMutation { get; init; }
    public bool PostIntegrityPassed { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
    public bool SchemaHardeningDeferred { get; init; } = true;
    public bool StudentsDeleted { get; init; }
    public bool MasterDataDeleted { get; init; }
}
