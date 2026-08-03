using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.Providers;

public sealed class RoomSwapRecommendationProvider : IConflictRecommendationProvider
{
    public string ProviderCode => "ROOM_SWAP";

    public bool CanHandle(ConflictResult conflict) =>
        conflict.Category == ConflictCategory.Room ||
        conflict.RuleCode is "ROOM_DOUBLE_BOOKING" or "ROOM_CAPACITY" or "ROOM_WRONG_TYPE"
            or "ROOM_WRONG_FEATURE" or "ROOM_LAB_REQUIREMENT" or "ROOM_UNAVAILABLE" or "ROOM_MAINTENANCE";

    public Task<IReadOnlyList<ConflictRecommendation>> RecommendAsync(
        ConflictResult conflict,
        ConflictAnalysisContext context,
        CancellationToken cancellationToken = default)
    {
        var options = new List<ResolutionOption>();
        var entry = context.Entries.FirstOrDefault(e => e.Id == conflict.TimetableEntryId);
        var occupied = context.Entries
            .Where(e => conflict.DayOfWeek.HasValue && e.DayOfWeek == conflict.DayOfWeek
                        && conflict.TimeSlotId.HasValue && e.TimeSlotId == conflict.TimeSlotId.Value)
            .Select(e => e.RoomId)
            .ToHashSet();

        var candidates = context.Rooms.Values
            .Where(r => !occupied.Contains(r.Id) && (!conflict.RoomId.HasValue || r.Id != conflict.RoomId.Value))
            .OrderByDescending(r => r.Capacity)
            .Take(5)
            .ToList();

        foreach (var room in candidates)
        {
            options.Add(new ResolutionOption
            {
                OptionCode = $"SWAP_ROOM_{room.Id}",
                Label = $"Use alternate room: {room.Name}",
                Description = $"Manually reassign the class to room '{room.Name}' (capacity {room.Capacity}). Advisory only — does not edit the timetable.",
                SuggestedRoomId = room.Id,
                NavigationPath = entry is null ? conflict.Recommendation.NavigationPath : context.Nav(entry)
            });
        }

        if (options.Count == 0)
        {
            options.Add(new ResolutionOption
            {
                OptionCode = "SWAP_ROOM_MANUAL",
                Label = "Swap room manually",
                Description = "Open the timetable cell and assign another available room for this period.",
                NavigationPath = conflict.Recommendation.NavigationPath
            });
        }

        options.Add(new ResolutionOption
        {
            OptionCode = "MOVE_CLASS_PERIOD",
            Label = "Move class to another period",
            Description = "Keep the room and move the class to a free period to avoid the room clash.",
            NavigationPath = conflict.Recommendation.NavigationPath
        });

        IReadOnlyList<ConflictRecommendation> list =
        [
            new ConflictRecommendation
            {
                RecommendationId = $"{ProviderCode}:{conflict.RuleCode}:{conflict.TimetableEntryId}",
                Title = "Room resolution guidance",
                Summary = "Suggested room swaps / alternate rooms. Teacher must apply changes manually.",
                ProviderCode = ProviderCode,
                Options = options,
                Score = new ResolutionScore
                {
                    Confidence = candidates.Count > 0 ? 0.78m : 0.45m,
                    Impact = ResolutionImpactLevel.Medium,
                    Difficulty = candidates.Count > 0 ? ResolutionDifficulty.Easy : ResolutionDifficulty.Moderate,
                    Rank = 1
                },
                Reasons =
                [
                    new ResolutionReason { Code = "RULE", Message = $"Triggered by {conflict.RuleName}." },
                    new ResolutionReason { Code = "SCOPE", Message = "Guidance only — Conflict Engine retains ownership of detection." }
                ],
                EstimatedResolution = candidates.Count > 0 ? "5–10 minutes (manual)" : "15–30 minutes (manual search)",
                NavigationPath = conflict.Recommendation.NavigationPath
            }
        ];

        return Task.FromResult(list);
    }
}
