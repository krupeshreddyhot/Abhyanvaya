using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.Providers;

public sealed class TimeSlotRecommendationProvider : IConflictRecommendationProvider
{
    public string ProviderCode => "TIME_SLOT";

    public bool CanHandle(ConflictResult conflict) =>
        conflict.Category is ConflictCategory.Student or ConflictCategory.Calendar or ConflictCategory.Faculty or ConflictCategory.Room;

    public Task<IReadOnlyList<ConflictRecommendation>> RecommendAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var entry = context.Entries.FirstOrDefault(e => e.Id == conflict.TimetableEntryId);
        var day = conflict.DayOfWeek ?? entry?.DayOfWeek ?? (byte)1;
        var staffId = conflict.StaffId ?? entry?.StaffId;
        var roomId = conflict.RoomId ?? entry?.RoomId;
        var groupId = conflict.GroupId ?? entry?.GroupId;

        var busySlots = context.Entries
            .Where(e => e.DayOfWeek == day &&
                        ((staffId.HasValue && e.StaffId == staffId) ||
                         (roomId.HasValue && e.RoomId == roomId) ||
                         (groupId.HasValue && e.GroupId == groupId)))
            .Select(e => e.TimeSlotId)
            .ToHashSet();

        var freeSlots = context.TimeSlots.Values
            .Where(s => s.SlotKind == SlotKind.Period && !busySlots.Contains(s.Id))
            .OrderBy(s => s.StartTime)
            .Take(5)
            .ToList();

        var options = freeSlots.Select(s => new ResolutionOption
        {
            OptionCode = $"MOVE_SLOT_{s.Id}",
            Label = $"Move period to {s.Name}",
            Description = $"Suggested free period '{s.Name}' ({s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm}) on day {day}. Apply manually in the designer.",
            SuggestedTimeSlotId = s.Id,
            SuggestedDayOfWeek = day,
            NavigationPath = conflict.Recommendation.NavigationPath
        }).ToList();

        options.Add(new ResolutionOption
        {
            OptionCode = "MOVE_TO_OTHER_DAY",
            Label = "Move class to another working day",
            Description = "Relocate the class to a working day with lower load for the same faculty/group/room.",
            NavigationPath = conflict.Recommendation.NavigationPath
        });

        if (conflict.Category == ConflictCategory.Calendar)
        {
            options.Insert(0, new ResolutionOption
            {
                OptionCode = "RESPECT_CALENDAR",
                Label = "Respect holiday / working-day calendar",
                Description = "Move the class off the holiday or non-working day indicated by academic calendar rules.",
                NavigationPath = conflict.Recommendation.NavigationPath
            });
        }

        IReadOnlyList<ConflictRecommendation> list =
        [
            new ConflictRecommendation
            {
                RecommendationId = $"{ProviderCode}:{conflict.RuleCode}:{conflict.TimetableEntryId}",
                Title = "Time-slot resolution guidance",
                Summary = "Suggested period/day moves. No optimizer and no automatic scheduling.",
                ProviderCode = ProviderCode,
                Options = options,
                Score = new ResolutionScore
                {
                    Confidence = freeSlots.Count > 0 ? 0.74m : 0.4m,
                    Impact = ResolutionImpactLevel.Medium,
                    Difficulty = freeSlots.Count > 0 ? ResolutionDifficulty.Easy : ResolutionDifficulty.Hard,
                    Rank = 2
                },
                Reasons =
                [
                    new ResolutionReason { Code = "FREE_SLOTS", Message = $"{freeSlots.Count} candidate free period(s) found in loaded context." },
                    new ResolutionReason { Code = "NO_AUTO", Message = "Suggestions never modify the timetable." }
                ],
                EstimatedResolution = freeSlots.Count > 0 ? "5–15 minutes (manual)" : "30+ minutes (requires redesign)",
                NavigationPath = conflict.Recommendation.NavigationPath
            }
        ];

        return Task.FromResult(list);
    }
}
