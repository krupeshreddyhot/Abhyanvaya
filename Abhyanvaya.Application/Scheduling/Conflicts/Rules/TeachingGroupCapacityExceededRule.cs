using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Rules;

/// <summary>
/// AI-SCHED-CAP Prompt 3 — Teaching Group MaxTeachingCapacity vs ResolvedStudentCount.
/// Soft/detect-only via ConflictEngine. Separate from ROOM_CAPACITY (physical room seats).
/// Prompt 4 — messaging via shared presentation composer.
/// </summary>
public sealed class TeachingGroupCapacityExceededRule : IConflictRule
{
    public const string Code = "TEACHING_GROUP_CAPACITY_EXCEEDED";

    public string RuleCode => Code;
    public string RuleName => "Teaching Group Capacity Exceeded";
    public ConflictCategory Category => ConflictCategory.Other;

    public Task AnalyzeAsync(
        ConflictAnalysisContext context,
        ConflictResultBag bag,
        CancellationToken cancellationToken = default)
    {
        foreach (var entry in context.Entries)
        {
            if (entry.TeachingGroupId is not int tgId)
                continue;

            if (!context.TeachingGroups.TryGetValue(tgId, out var tg))
                continue;

            // MaxTeachingCapacity null → no configured teaching capacity → no conflict.
            if (tg.MaxTeachingCapacity is not int maxCap)
                continue;

            // Domain: MaxTeachingCapacity = 0 is invalid; only positive maxima are evaluated.
            if (maxCap <= 0)
                continue;

            // ResolvedStudentCount unavailable → do not invent; skip TG capacity rule.
            if (!context.ResolvedStudentCountsByTeachingGroupId.TryGetValue(tgId, out var resolved))
                continue;

            if (resolved <= maxCap)
                continue;

            var (description, why, action) =
                SchedulingConflictPresentationComposer.Instance.TeachingGroupCapacityCopy(tg, resolved, maxCap);

            bag.Add(context.Create(
                this,
                ConflictSeverity.Error,
                description,
                why,
                action,
                entry));
        }

        return Task.CompletedTask;
    }
}
