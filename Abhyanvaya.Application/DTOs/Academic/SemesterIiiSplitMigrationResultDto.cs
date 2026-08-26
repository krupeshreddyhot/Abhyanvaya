namespace Abhyanvaya.Application.DTOs.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B — Semester III split + Student remap result.</summary>
public sealed class SemesterIiiSplitMigrationResultDto
{
    public string Status { get; init; } = null!; // Completed | AlreadyCompleted | Aborted
    public bool RolledBack { get; init; }
    public string? AbortReason { get; init; }
    public int SourceSemesterId { get; init; }
    public int FinanceGroupId { get; init; }
    public int CaGroupId { get; init; }
    public int FinanceTargetSemesterId { get; init; }
    public int CaTargetSemesterId { get; init; }
    public bool FinanceSemesterCreated { get; init; }
    public bool CaSemesterCreated { get; init; }
    public bool FinanceSemesterReused { get; init; }
    public bool CaSemesterReused { get; init; }
    public int FinanceStudentsRemapped { get; init; }
    public int CaStudentsRemapped { get; init; }
    public int TotalStudentsRemapped { get; init; }
    public int UnresolvedStudents { get; init; }
    public int DownstreamAttendanceReferences { get; init; }
    public int DownstreamSubjectReferences { get; init; }
    public int DownstreamSectionReferences { get; init; }
    public int DownstreamSubjectAllocationReferences { get; init; }
    public int DownstreamTimetableEntryReferences { get; init; }
    public int DownstreamTeachingGroupReferences { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}
