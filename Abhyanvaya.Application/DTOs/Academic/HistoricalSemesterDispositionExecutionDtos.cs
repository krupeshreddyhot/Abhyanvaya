namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3K-B (package 3KB) / PromptCode P1-4-3KB —
/// Controlled HISTORICAL_ARCHIVE execution for ARCHIVE_ELIGIBLE Semesters only.
/// </summary>
public static class HistoricalSemesterDispositionExecutionCodes
{
    public const string PromptCode = "P1-4-3KB";
    public const string HistoricalArchive = "HISTORICAL_ARCHIVE";
}

public sealed class HistoricalSemesterDispositionExecuteRequest
{
    /// <summary>Must be HISTORICAL_ARCHIVE.</summary>
    public string Disposition { get; set; } = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive;

    /// <summary>Explicit execution scope. Empty is rejected (no archive-all).</summary>
    public List<int> SemesterIds { get; set; } = [];

    public string? Reason { get; set; }
}

public sealed class HistoricalSemesterDispositionExecuteResultDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public string PromptCode { get; init; } = HistoricalSemesterDispositionExecutionCodes.PromptCode;
    public string Disposition { get; init; } = HistoricalSemesterDispositionExecutionCodes.HistoricalArchive;
    public string CorrelationId { get; init; } = "";
    public bool IsSuccessful { get; init; }
    public string ExecutionStatus { get; init; } = "";
    public bool RolledBack { get; init; }
    public bool TransactionCommitted { get; init; }
    public string TransactionModel { get; init; } = "ALL_OR_NOTHING";
    public string? AbortReason { get; init; }
    public string? ConcurrencyResult { get; init; }

    public int Requested { get; init; }
    public int Archived { get; init; }
    public int AlreadyComplete { get; init; }
    public int Rejected { get; init; }
    public int Blocked { get; init; }

    public IReadOnlyList<HistoricalSemesterDispositionExecuteItemResultDto> Results { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];
    public bool SchemaHardeningDeferred { get; init; } = true;
    public bool GroupIdInvented { get; init; }
    public bool DownstreamEntitiesMutated { get; init; }
}

public sealed class HistoricalSemesterDispositionExecuteItemResultDto
{
    public int SemesterId { get; init; }
    public string Result { get; init; } = "";
    public string Classification { get; init; } = "";
    public int? GroupIdBefore { get; init; }
    public int? GroupIdAfter { get; init; }
    public bool IsHistoricalArchiveAfter { get; init; }
    public bool SemesterRowMutated { get; init; }
    public bool JournalWritten { get; init; }
    public string Reason { get; init; } = "";
}
