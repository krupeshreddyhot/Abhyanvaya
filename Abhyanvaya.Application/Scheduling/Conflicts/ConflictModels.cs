using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts;

public sealed class ConflictRecommendation
{
    public required string SuggestedResolution { get; init; }
    public string? NavigationPath { get; init; }
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
}

public sealed class ConflictResult
{
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public ConflictCategory Category { get; init; }
    public ConflictSeverity Severity { get; init; }
    public required string Description { get; init; }
    public required string WhyOccurred { get; init; }
    public required ConflictRecommendation Recommendation { get; init; }
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
    public int? RelatedEntryId { get; init; }
    public byte? DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
    public int? StaffId { get; init; }
    public int? RoomId { get; init; }
    public int? DepartmentId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? SubjectId { get; init; }
}

public sealed class ConflictResultBag
{
    private readonly List<ConflictResult> _items = [];
    public IReadOnlyList<ConflictResult> Items => _items;

    public void Add(ConflictResult result) => _items.Add(result);

    public ConflictSummary BuildSummary(int runId, int academicYearId, int? timetableId, int? departmentId, DateTime startedUtc, string triggerSource)
    {
        return new ConflictSummary
        {
            RunId = runId,
            TimetableId = timetableId,
            AcademicYearId = academicYearId,
            DepartmentId = departmentId,
            StartedUtc = startedUtc,
            CompletedUtc = DateTime.UtcNow,
            Status = "Completed",
            TriggerSource = triggerSource,
            TotalConflicts = _items.Count,
            FacultyCount = _items.Count(x => x.Category == ConflictCategory.Faculty),
            RoomCount = _items.Count(x => x.Category == ConflictCategory.Room),
            StudentCount = _items.Count(x => x.Category == ConflictCategory.Student),
            CalendarCount = _items.Count(x => x.Category == ConflictCategory.Calendar),
            CriticalCount = _items.Count(x => x.Severity == ConflictSeverity.Critical),
            ErrorCount = _items.Count(x => x.Severity == ConflictSeverity.Error),
            WarningCount = _items.Count(x => x.Severity == ConflictSeverity.Warning),
            InformationCount = _items.Count(x => x.Severity == ConflictSeverity.Information),
        };
    }
}

public sealed class ConflictSummary
{
    public int RunId { get; init; }
    public int? TimetableId { get; init; }
    public int AcademicYearId { get; init; }
    public int? DepartmentId { get; init; }
    public DateTime StartedUtc { get; init; }
    public DateTime? CompletedUtc { get; init; }
    public string Status { get; init; } = "";
    public string TriggerSource { get; init; } = "";
    public int TotalConflicts { get; init; }
    public int FacultyCount { get; init; }
    public int RoomCount { get; init; }
    public int StudentCount { get; init; }
    public int CalendarCount { get; init; }
    public int CriticalCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int InformationCount { get; init; }
    public bool BlocksEditing => false;
}

public interface IConflictRule
{
    string RuleCode { get; }
    string RuleName { get; }
    ConflictCategory Category { get; }
    Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default);
}
