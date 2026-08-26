namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B-A — post-migration integrity audit (read-only).</summary>
public enum IntegritySeverity
{
    Critical = 1,
    Error = 2,
    Warning = 3,
}

public sealed class SemesterPostMigrationIntegrityAuditDto
{
    public DateTime GeneratedUtc { get; init; }
    public int TenantId { get; init; }
    public bool IsReadOnly { get; init; } = true;
    public bool IsHealthy { get; init; }
    public SemesterPostMigrationIntegritySummaryDto Summary { get; init; } = new();
    public IReadOnlyList<SemesterPostMigrationIntegrityCheckDto> Checks { get; init; } = [];
    public IReadOnlyList<LegacySemesterStatusDto> LegacySemesters { get; init; } = [];
    public IReadOnlyList<SemesterPostMigrationIntegrityViolationDto> Violations { get; init; } = [];
    public SemesterIiiSplitVerificationDto SemesterIiiSplit { get; init; } = new();
    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class SemesterPostMigrationIntegritySummaryDto
{
    public int Critical { get; init; }
    public int Errors { get; init; }
    public int Warnings { get; init; }
}

public sealed class SemesterPostMigrationIntegrityCheckDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Result { get; init; } = null!; // PASS | FAIL | WARN
    public int ViolationCount { get; init; }
}

public sealed class SemesterPostMigrationIntegrityViolationDto
{
    public string Code { get; init; } = null!;
    public IntegritySeverity Severity { get; init; }
    public string SeverityCode { get; init; } = null!;
    public string Message { get; init; } = null!;
    public string? EntityId { get; init; }
    public string? EntityType { get; init; }
    public int? SemesterId { get; init; }
    public int? GroupId { get; init; }
    public int? CourseId { get; init; }
}

public sealed class LegacySemesterStatusDto
{
    public int SemesterId { get; init; }
    public int CourseId { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = null!;
    public string Classification { get; init; } = null!;
    public int StudentCount { get; init; }
    public int DownstreamReferenceTotal { get; init; }
}

public sealed class SemesterIiiSplitVerificationDto
{
    public bool FinanceSemesterIiiExists { get; init; }
    public bool CaSemesterIiiExists { get; init; }
    public int? FinanceSemesterId { get; init; }
    public int? CaSemesterId { get; init; }
    public int? LegacySemesterIiiId { get; init; }
    public int StudentsOnLegacySemesterIii { get; init; }
    public int FinanceStudentsOnTarget { get; init; }
    public int CaStudentsOnTarget { get; init; }
    public bool MigratedStudentsFullyRemapped { get; init; }
}
