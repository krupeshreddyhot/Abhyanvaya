using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

public sealed class ConflictExplanation
{
    public required string RuleCode { get; init; }
    public required string RuleName { get; init; }
    public required string RuleCategory { get; init; }
    public required string RuleDescription { get; init; }
    public required string BusinessReason { get; init; }
    public ConflictSeverity Severity { get; init; }
    public int Priority { get; init; }
    public required string WhyTriggered { get; init; }
    public required string SuggestedAction { get; init; }
    public required string Impact { get; init; }
    public required IReadOnlyList<string> References { get; init; }
    public string? NavigationPath { get; init; }
    public int? TimetableId { get; init; }
    public int? TimetableEntryId { get; init; }
}

public interface IConflictExplainabilityService
{
    ConflictExplanation Explain(ConflictResult conflict, ImpactGraph? impact = null);
}

public sealed class ConflictExplainabilityService : IConflictExplainabilityService
{
    private static readonly IReadOnlyDictionary<string, string> BusinessReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FACULTY_DOUBLE_BOOKING"] = "A faculty member cannot teach two classes in the same period.",
        ["FACULTY_AVAILABILITY"] = "Teaching assignments must respect approved leave and unavailability.",
        ["FACULTY_MAX_CONTINUOUS"] = "Continuous teaching load must stay within agreed ergonomic limits.",
        ["FACULTY_BREAK_VIOLATION"] = "Faculty rest between consecutive classes protects teaching quality.",
        ["FACULTY_CROSS_CAMPUS"] = "Cross-campus travel requires a minimum transition buffer.",
        ["FACULTY_LUNCH_VIOLATION"] = "Lunch windows are protected academic calendar periods.",
        ["ROOM_DOUBLE_BOOKING"] = "A physical room can host only one class per period.",
        ["ROOM_CAPACITY"] = "Room capacity must accommodate expected class size.",
        ["STUDENT_GROUP_OVERLAP"] = "A student group cannot attend two classes at once.",
        ["CALENDAR_HOLIDAY"] = "Classes must not be scheduled on configured holidays.",
    };

    public ConflictExplanation Explain(ConflictResult conflict, ImpactGraph? impact = null)
    {
        var category = conflict.Category.ToString();
        var business = BusinessReasons.TryGetValue(conflict.RuleCode, out var br)
            ? br
            : $"Business scheduling policy '{conflict.RuleName}' was violated.";

        var impactText = impact is null
            ? "Open Impact Panel for faculty, students, rooms, departments, versions, workload, availability, and attendance signals."
            : $"Risk {impact.Summary.RiskLevel}: faculty={impact.Summary.FacultyAffected}, students={impact.Summary.StudentsAffected}, rooms={impact.Summary.RoomsAffected}, publishedVersions={impact.Summary.PublishedVersionsAffected}.";

        return new ConflictExplanation
        {
            RuleCode = conflict.RuleCode,
            RuleName = conflict.RuleName,
            RuleCategory = category,
            RuleDescription = conflict.Description,
            BusinessReason = business,
            Severity = conflict.Severity,
            Priority = PriorityOf(conflict.Severity),
            WhyTriggered = conflict.WhyOccurred,
            SuggestedAction = conflict.Recommendation.SuggestedResolution,
            Impact = impactText,
            References =
            [
                "AI30 Phase 2B Enterprise Conflict Engine",
                "AI30 Phase 2B.5 Conflict Intelligence",
                $"Rule:{conflict.RuleCode}"
            ],
            NavigationPath = conflict.Recommendation.NavigationPath,
            TimetableId = conflict.TimetableId,
            TimetableEntryId = conflict.TimetableEntryId
        };
    }

    private static int PriorityOf(ConflictSeverity severity) => severity switch
    {
        ConflictSeverity.Critical => 1,
        ConflictSeverity.Error => 2,
        ConflictSeverity.Warning => 3,
        _ => 4
    };
}
