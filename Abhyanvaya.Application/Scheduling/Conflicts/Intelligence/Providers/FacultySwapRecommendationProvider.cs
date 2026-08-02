using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.Providers;

public sealed class FacultySwapRecommendationProvider : IConflictRecommendationProvider
{
    public string ProviderCode => "FACULTY_SWAP";

    public bool CanHandle(ConflictResult conflict) =>
        conflict.Category == ConflictCategory.Faculty ||
        conflict.RuleCode.StartsWith("FACULTY_", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ConflictRecommendation>> RecommendAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var busyStaff = context.Entries
            .Where(e => conflict.DayOfWeek.HasValue && e.DayOfWeek == conflict.DayOfWeek
                        && conflict.TimeSlotId.HasValue && e.TimeSlotId == conflict.TimeSlotId.Value)
            .Select(e => e.StaffId)
            .ToHashSet();

        var alternateStaff = context.StaffNames
            .Where(kv => !busyStaff.Contains(kv.Key) && (!conflict.StaffId.HasValue || kv.Key != conflict.StaffId.Value))
            .Take(5)
            .ToList();

        var options = alternateStaff.Select(s => new ResolutionOption
        {
            OptionCode = $"SWAP_FACULTY_{s.Key}",
            Label = $"Swap faculty: {s.Value}",
            Description = $"Consider reassigning to {s.Value} who appears free in this slot (advisory — verify allocation eligibility).",
            SuggestedStaffId = s.Key,
            NavigationPath = conflict.Recommendation.NavigationPath
        }).ToList();

        options.Add(new ResolutionOption
        {
            OptionCode = "MOVE_FACULTY_PERIOD",
            Label = "Move period for this faculty",
            Description = "Move one of the overlapping classes to another free period for the same faculty.",
            NavigationPath = conflict.Recommendation.NavigationPath
        });

        if (conflict.RuleCode is "FACULTY_MAX_CONTINUOUS" or "FACULTY_BREAK_VIOLATION" or "FACULTY_LUNCH_VIOLATION")
        {
            options.Insert(0, new ResolutionOption
            {
                OptionCode = "INSERT_BREAK",
                Label = "Insert break / redistribute load",
                Description = "Redistribute consecutive classes to restore minimum break / continuous-class limits.",
                NavigationPath = conflict.Recommendation.NavigationPath
            });
        }

        if (conflict.RuleCode == "FACULTY_CROSS_CAMPUS")
        {
            options.Insert(0, new ResolutionOption
            {
                OptionCode = "AVOID_CROSS_CAMPUS",
                Label = "Avoid consecutive cross-campus travel",
                Description = $"Ensure at least {context.Thresholds.FacultyTravelBufferMinutes} minutes between campus changes, or keep both classes on one campus.",
                NavigationPath = conflict.Recommendation.NavigationPath
            });
        }

        IReadOnlyList<ConflictRecommendation> list =
        [
            new ConflictRecommendation
            {
                RecommendationId = $"{ProviderCode}:{conflict.RuleCode}:{conflict.TimetableEntryId}",
                Title = "Faculty resolution guidance",
                Summary = "Suggested faculty swaps or period moves. No automatic reassignment is performed.",
                ProviderCode = ProviderCode,
                Options = options,
                Score = new ResolutionScore
                {
                    Confidence = alternateStaff.Count > 0 ? 0.72m : 0.5m,
                    Impact = ResolutionImpactLevel.High,
                    Difficulty = ResolutionDifficulty.Moderate,
                    Rank = 1
                },
                Reasons =
                [
                    new ResolutionReason { Code = "RULE", Message = conflict.WhyOccurred },
                    new ResolutionReason { Code = "ADVISORY", Message = "User remains in control; timetable is never auto-edited." }
                ],
                EstimatedResolution = "10–20 minutes (manual)",
                NavigationPath = conflict.Recommendation.NavigationPath
            }
        ];

        return Task.FromResult(list);
    }
}
