using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Rules;

public sealed class StudentGroupOverlapRule : IConflictRule
{
    public string RuleCode => "STUDENT_GROUP_OVERLAP";
    public string RuleName => "Group Overlap";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var group in context.Entries.GroupBy(e => new { e.GroupId, e.DayOfWeek, e.TimeSlotId }).Where(g => g.Count() > 1))
        {
            var list = group.ToList();
            foreach (var entry in list)
            {
                var other = list.First(x => x.Id != entry.Id);
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Critical,
                    "Student group has overlapping classes in the same period.",
                    $"Group {entry.GroupId} appears in entries {entry.Id} and {other.Id}.",
                    "Move one of the overlapping group sessions.",
                    entry,
                    other.Id));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class StudentSemesterOverlapRule : IConflictRule
{
    public string RuleCode => "STUDENT_SEMESTER_OVERLAP";
    public string RuleName => "Semester Overlap";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var group in context.Entries
                     .GroupBy(e => new { e.CourseId, e.SemesterId, e.DayOfWeek, e.TimeSlotId })
                     .Where(g => g.Select(x => x.GroupId).Distinct().Count() > 1 && g.Count() > 1))
        {
            // Same course+semester cohort clash across groups in same slot is informational when groups differ intentionally
            foreach (var entry in group)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Warning,
                    "Multiple groups in the same course/semester share this period.",
                    $"Course {entry.CourseId}, semester {entry.SemesterId}, slot {entry.TimeSlotId}.",
                    "Confirm this is intentional (e.g. combined lecture); otherwise stagger groups.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class StudentDuplicateSubjectRule : IConflictRule
{
    public string RuleCode => "STUDENT_DUPLICATE_SUBJECT";
    public string RuleName => "Duplicate Subject";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var group in context.Entries
                     .GroupBy(e => new { e.GroupId, e.SemesterId, e.SubjectId, e.DayOfWeek, e.TimeSlotId })
                     .Where(g => g.Count() > 1))
        {
            foreach (var entry in group)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Error,
                    "Duplicate subject session for the same student group in one period.",
                    $"Subject {entry.SubjectId} appears more than once for group {entry.GroupId}.",
                    "Remove the duplicate entry.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class StudentElectiveOverlapRule : IConflictRule
{
    public string RuleCode => "STUDENT_ELECTIVE_OVERLAP";
    public string RuleName => "Elective Overlap";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.Subjects.TryGetValue(entry.SubjectId, out var subject)) continue;
            var isElective = subject.IsElective;
            if (!isElective && subject.DeliveryTypeId.HasValue
                && context.DeliveryTypes.TryGetValue(subject.DeliveryTypeId.Value, out var delivery)
                && (delivery.Code?.ToUpperInvariant().Contains("ELECT") ?? false))
                isElective = true;
            if (!isElective) continue;
            context.DeliveryTypes.TryGetValue(subject.DeliveryTypeId ?? -1, out var deliveryInfo);
            var electiveLabel = deliveryInfo?.Name ?? "Elective";

            var overlaps = context.Entries.Where(e =>
                e.Id != entry.Id &&
                e.GroupId == entry.GroupId &&
                e.DayOfWeek == entry.DayOfWeek &&
                e.TimeSlotId == entry.TimeSlotId).ToList();
            if (overlaps.Count == 0) continue;

            bag.Add(context.Create(
                this,
                ConflictSeverity.Warning,
                "Elective subject overlaps another session for the same group.",
                $"Elective '{electiveLabel}' conflicts with entry {overlaps[0].Id}.",
                "Offer electives in non-overlapping slots for the same cohort.",
                entry,
                overlaps[0].Id));
        }
        return Task.CompletedTask;
    }
}

public sealed class StudentBatchConflictRule : IConflictRule
{
    public string RuleCode => "STUDENT_BATCH_CONFLICT";
    public string RuleName => "Batch Conflict";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        // Batch ≈ Course+Group+Semester identity collision across rooms same slot
        foreach (var group in context.Entries
                     .GroupBy(e => new { e.CourseId, e.GroupId, e.SemesterId, e.DayOfWeek, e.TimeSlotId })
                     .Where(g => g.Select(x => x.RoomId).Distinct().Count() > 1))
        {
            foreach (var entry in group)
            {
                bag.Add(context.Create(
                    this,
                    ConflictSeverity.Error,
                    "Same student batch is scheduled in multiple rooms at once.",
                    "Course/group/semester identity appears in more than one room for the same slot.",
                    "Keep a batch in a single room per period unless split intentionally with different subjects and no overlap.",
                    entry));
            }
        }
        return Task.CompletedTask;
    }
}

public sealed class StudentPracticalConflictRule : IConflictRule
{
    public string RuleCode => "STUDENT_PRACTICAL_CONFLICT";
    public string RuleName => "Practical Conflict";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        EmitDeliveryOverlap(context, bag, this, "PRACT", "Practical");
        return Task.CompletedTask;
    }

    internal static void EmitDeliveryOverlap(
        ConflictAnalysisContext context,
        ConflictResultBag bag,
        IConflictRule rule,
        string token,
        string label)
    {
        foreach (var entry in context.Entries)
        {
            if (!context.Subjects.TryGetValue(entry.SubjectId, out var subject)) continue;
            if (!context.DeliveryTypes.TryGetValue(subject.DeliveryTypeId ?? -1, out var delivery)) continue;
            if (!(delivery.Code?.ToUpperInvariant().Contains(token) ?? false) &&
                !(delivery.Name?.ToUpperInvariant().Contains(token) ?? false))
                continue;

            var clash = context.Entries.FirstOrDefault(e =>
                e.Id != entry.Id &&
                e.GroupId == entry.GroupId &&
                e.DayOfWeek == entry.DayOfWeek &&
                e.TimeSlotId == entry.TimeSlotId);
            if (clash is null) continue;

            bag.Add(context.Create(
                rule,
                ConflictSeverity.Warning,
                $"{label} session overlaps another class for the same group.",
                $"{label} delivery '{delivery.Name}' conflicts with entry {clash.Id}.",
                $"Schedule {label.ToLowerInvariant()} sessions without overlapping core classes.",
                entry,
                clash.Id));
        }
    }
}

public sealed class StudentTutorialConflictRule : IConflictRule
{
    public string RuleCode => "STUDENT_TUTORIAL_CONFLICT";
    public string RuleName => "Tutorial Conflict";
    public ConflictCategory Category => ConflictCategory.Student;

    public Task AnalyzeAsync(ConflictAnalysisContext context, ConflictResultBag bag, CancellationToken cancellationToken = default)
    {
        StudentPracticalConflictRule.EmitDeliveryOverlap(context, bag, this, "TUT", "Tutorial");
        return Task.CompletedTask;
    }
}
