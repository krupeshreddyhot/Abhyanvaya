using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Sandbox;

public interface IScenarioHistoryService
{
    Task RecordAsync(int scenarioPk, ScenarioHistoryAction action, string? details, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScenarioHistoryDto>> ListAsync(int scenarioPk, CancellationToken cancellationToken = default);
}

public sealed class ScenarioHistoryService : IScenarioHistoryService
{
    private readonly IOptimizationScenarioRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public ScenarioHistoryService(IOptimizationScenarioRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task RecordAsync(int scenarioPk, ScenarioHistoryAction action, string? details, CancellationToken cancellationToken = default)
    {
        await _repo.AddHistoryAsync(new OptimizationScenarioHistory
        {
            TenantId = _currentUser.TenantId,
            OptimizationScenarioId = scenarioPk,
            Action = action,
            ActorUserId = _currentUser.UserId,
            Details = details,
            OccurredUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ScenarioHistoryDto>> ListAsync(int scenarioPk, CancellationToken cancellationToken = default)
    {
        var rows = await _repo.ListHistoryAsync(scenarioPk, cancellationToken);
        return rows.Select(h => new ScenarioHistoryDto
        {
            Action = h.Action,
            ActionName = h.Action.ToString(),
            ActorUserId = h.ActorUserId,
            Details = h.Details,
            OccurredUtc = h.OccurredUtc
        }).ToList();
    }
}

public interface IReplayService
{
    Task<ReplayTimelineDto> GetTimelineAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ReplayTimelineDto> ReplayAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ReplayTimelineDto> RestartAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ReplayComparisonDto> CompareSnapshotsAsync(Guid scenarioId, Guid leftSnapshotId, Guid rightSnapshotId, CancellationToken cancellationToken = default);
}

/// <summary>Read-only simulation replay. Never mutates production timetables or immutable snapshots.</summary>
public sealed class ReplayService : IReplayService
{
    private readonly IOptimizationScenarioRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IScenarioHistoryService _history;

    public ReplayService(IOptimizationScenarioRepository repo, ICurrentUserService currentUser, IScenarioHistoryService history)
    {
        _repo = repo;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<ReplayTimelineDto> GetTimelineAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        return BuildTimeline(scenario);
    }

    public async Task<ReplayTimelineDto> ReplayAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.ReplayCount += 1;
        scenario.LastReplayedUtc = DateTime.UtcNow;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Replayed, "Read-only replay", cancellationToken);
        return BuildTimeline(scenario);
    }

    public Task<ReplayTimelineDto> RestartAsync(Guid scenarioId, CancellationToken cancellationToken = default) =>
        ReplayAsync(scenarioId, cancellationToken);

    public async Task<ReplayComparisonDto> CompareSnapshotsAsync(
        Guid scenarioId,
        Guid leftSnapshotId,
        Guid rightSnapshotId,
        CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        var left = scenario.Snapshots.FirstOrDefault(s => s.SnapshotId == leftSnapshotId)
            ?? throw new KeyNotFoundException("Left snapshot not found.");
        var right = scenario.Snapshots.FirstOrDefault(s => s.SnapshotId == rightSnapshotId)
            ?? throw new KeyNotFoundException("Right snapshot not found.");

        var leftDto = ToReplaySnapshot(left, scenario);
        var rightDto = ToReplaySnapshot(right, scenario);
        return new ReplayComparisonDto
        {
            Left = leftDto,
            Right = rightDto,
            ScoreDelta = rightDto.Score - leftDto.Score,
            ConflictDelta = rightDto.ConflictCount - leftDto.ConflictCount,
            Notes = "Read-only replay comparison — no production changes."
        };
    }

    private async Task<OptimizationScenario> RequireAsync(Guid scenarioId, CancellationToken cancellationToken) =>
        await _repo.GetByScenarioIdAsync(_currentUser.TenantId, scenarioId, true, cancellationToken)
        ?? throw new KeyNotFoundException("Scenario not found.");

    private static ReplayTimelineDto BuildTimeline(OptimizationScenario scenario) => new()
    {
        ScenarioId = scenario.ScenarioId,
        IsReadOnly = true,
        Steps = scenario.Snapshots.OrderBy(s => s.Sequence).Select(s => ToReplaySnapshot(s, scenario)).ToList()
    };

    private static ReplaySnapshotDto ToReplaySnapshot(OptimizationSnapshot snap, OptimizationScenario scenario)
    {
        var score = scenario.CurrentScore;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(snap.ScoresJson) ? "{}" : snap.ScoresJson);
            if (doc.RootElement.TryGetProperty("CurrentScore", out var cs) && cs.TryGetDecimal(out var v))
                score = v;
        }
        catch { /* keep scenario score */ }

        return new ReplaySnapshotDto
        {
            SnapshotId = snap.SnapshotId,
            Sequence = snap.Sequence,
            Label = snap.Label,
            Score = score,
            ConflictCount = scenario.ConflictCount,
            CapturedUtc = snap.CapturedUtc
        };
    }
}

public interface IScenarioComparisonService
{
    Task<ScenarioComparisonResultDto> CompareAsync(CompareScenariosRequest request, CancellationToken cancellationToken = default);
}

public sealed class ScenarioComparisonService : IScenarioComparisonService
{
    private readonly IOptimizationScenarioRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IScenarioHistoryService _history;

    public ScenarioComparisonService(
        IOptimizationScenarioRepository repo,
        ICurrentUserService currentUser,
        IScenarioHistoryService history)
    {
        _repo = repo;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<ScenarioComparisonResultDto> CompareAsync(CompareScenariosRequest request, CancellationToken cancellationToken = default)
    {
        var left = await _repo.GetByScenarioIdAsync(_currentUser.TenantId, request.LeftScenarioId, true, cancellationToken)
            ?? throw new KeyNotFoundException("Left scenario not found.");
        var right = await _repo.GetByScenarioIdAsync(_currentUser.TenantId, request.RightScenarioId, true, cancellationToken)
            ?? throw new KeyNotFoundException("Right scenario not found.");

        left.ComparisonCount += 1;
        right.ComparisonCount += 1;
        left.LastComparedUtc = DateTime.UtcNow;
        right.LastComparedUtc = DateTime.UtcNow;
        if (left.Status is ScenarioStatus.Draft or ScenarioStatus.Saved)
            left.Status = ScenarioStatus.Compared;
        if (right.Status is ScenarioStatus.Draft or ScenarioStatus.Saved)
            right.Status = ScenarioStatus.Compared;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(left.Id, ScenarioHistoryAction.Compared, $"Compared with {right.ScenarioId}", cancellationToken);
        await _history.RecordAsync(right.Id, ScenarioHistoryAction.Compared, $"Compared with {left.ScenarioId}", cancellationToken);

        var leftSnap = left.Snapshots.OrderByDescending(s => s.Sequence).FirstOrDefault();
        var rightSnap = right.Snapshots.OrderByDescending(s => s.Sequence).FirstOrDefault();
        var scoreDelta = right.ProjectedScore - left.ProjectedScore;
        var conflictDelta = right.ConflictCount - left.ConflictCount;

        var highlights = new List<string>();
        if (scoreDelta > 0) highlights.Add($"Right improves projected score by {scoreDelta:0.##}.");
        if (scoreDelta < 0) highlights.Add($"Left has higher projected score by {-scoreDelta:0.##}.");
        if (conflictDelta < 0) highlights.Add($"Right reduces conflicts by {-conflictDelta}.");
        if (conflictDelta > 0) highlights.Add($"Left has fewer conflicts by {conflictDelta}.");
        if (highlights.Count == 0) highlights.Add("No material score/conflict difference detected.");

        return new ScenarioComparisonResultDto
        {
            Left = Map(left),
            Right = Map(right),
            Differences = new DifferenceSummaryDto
            {
                ScoreDelta = left.CurrentScore - right.CurrentScore,
                ProjectedScoreDelta = scoreDelta,
                ConflictDelta = conflictDelta,
                Verdict = scoreDelta >= 0 && conflictDelta <= 0
                    ? "Right scenario looks equal or better on score/conflicts (preview only)."
                    : "Review highlights — no apply available in Phase 2B.7."
            },
            LeftMetrics = ParseMetrics(leftSnap?.MetricsJson),
            RightMetrics = ParseMetrics(rightSnap?.MetricsJson),
            LeftConflictSummaryJson = leftSnap?.ConflictSummaryJson ?? "{}",
            RightConflictSummaryJson = rightSnap?.ConflictSummaryJson ?? "{}",
            LeftRecommendationsJson = leftSnap?.RecommendationsJson ?? "[]",
            RightRecommendationsJson = rightSnap?.RecommendationsJson ?? "[]",
            ImprovementHighlights = highlights
        };
    }

    private static ScenarioSummaryDto Map(OptimizationScenario s) => new()
    {
        ScenarioId = s.ScenarioId,
        Id = s.Id,
        Name = s.Name,
        Status = s.Status,
        Owner = new ScenarioOwnerDto { UserId = s.OwnerUserId, DisplayName = $"User {s.OwnerUserId}" },
        AcademicYearId = s.AcademicYearId,
        DepartmentId = s.DepartmentId,
        SemesterId = s.SemesterId,
        TimetableId = s.TimetableId,
        IsFavorite = s.IsFavorite,
        IsPinned = s.IsPinned,
        IsTemplate = s.IsTemplate,
        IsImmutable = s.IsImmutable,
        Category = s.Category,
        Tags = string.IsNullOrWhiteSpace(s.TagsCsv) ? [] : s.TagsCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        CurrentScore = s.CurrentScore,
        ProjectedScore = s.ProjectedScore,
        ConflictCount = s.ConflictCount,
        ReplayCount = s.ReplayCount,
        ComparisonCount = s.ComparisonCount,
        ViewCount = s.ViewCount,
        SnapshotCount = s.Snapshots.Count,
        CreatedUtc = s.CreatedDate,
        LastReplayedUtc = s.LastReplayedUtc,
        ModifiesProductionTimetable = false
    };

    private static IReadOnlyList<OptimizationMetricDto> ParseMetrics(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return [];
        try
        {
            return JsonSerializer.Deserialize<List<OptimizationMetricDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public interface IScenarioCollaborationService
{
    Task<ScenarioNoteDto> AddNoteAsync(AddScenarioNoteRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioCommentDto> AddCommentAsync(AddScenarioCommentRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioBookmarkDto> AddBookmarkAsync(Guid scenarioId, string name, CancellationToken cancellationToken = default);
    Task<ScenarioApprovalDto> RequestApprovalAsync(RequestScenarioApprovalRequest request, CancellationToken cancellationToken = default);
    Task ShareReadOnlyAsync(ShareScenarioRequest request, CancellationToken cancellationToken = default);
    Task MarkReviewedAsync(Guid scenarioId, CancellationToken cancellationToken = default);
}

public sealed class ScenarioCollaborationService : IScenarioCollaborationService
{
    private readonly IOptimizationScenarioRepository _repo;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IScenarioHistoryService _history;

    public ScenarioCollaborationService(
        IOptimizationScenarioRepository repo,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IScenarioHistoryService history)
    {
        _repo = repo;
        _db = db;
        _currentUser = currentUser;
        _history = history;
    }

    public async Task<ScenarioNoteDto> AddNoteAsync(AddScenarioNoteRequest request, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(request.ScenarioId, cancellationToken);
        var note = new OptimizationScenarioNote
        {
            TenantId = _currentUser.TenantId,
            OptimizationScenarioId = scenario.Id,
            UserId = _currentUser.UserId,
            NoteText = request.NoteText.Trim(),
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(note);
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Commented, "Note added", cancellationToken);
        return new ScenarioNoteDto { Id = note.Id, UserId = note.UserId, NoteText = note.NoteText, CreatedUtc = note.CreatedDate };
    }

    public async Task<ScenarioCommentDto> AddCommentAsync(AddScenarioCommentRequest request, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(request.ScenarioId, cancellationToken);
        var comment = new OptimizationScenarioComment
        {
            TenantId = _currentUser.TenantId,
            OptimizationScenarioId = scenario.Id,
            UserId = _currentUser.UserId,
            CommentText = request.CommentText.Trim(),
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(comment);
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Commented, "Comment added", cancellationToken);
        return new ScenarioCommentDto { Id = comment.Id, UserId = comment.UserId, CommentText = comment.CommentText, CreatedUtc = comment.CreatedDate };
    }

    public async Task<ScenarioBookmarkDto> AddBookmarkAsync(Guid scenarioId, string name, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        var bookmark = new OptimizationScenarioBookmark
        {
            TenantId = _currentUser.TenantId,
            OptimizationScenarioId = scenario.Id,
            UserId = _currentUser.UserId,
            Name = string.IsNullOrWhiteSpace(name) ? scenario.Name : name.Trim(),
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(bookmark);
        await _repo.SaveChangesAsync(cancellationToken);
        return new ScenarioBookmarkDto { Id = bookmark.Id, Name = bookmark.Name };
    }

    public async Task<ScenarioApprovalDto> RequestApprovalAsync(RequestScenarioApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(request.ScenarioId, cancellationToken);
        var row = new OptimizationScenarioApprovalRequest
        {
            TenantId = _currentUser.TenantId,
            OptimizationScenarioId = scenario.Id,
            RequestedByUserId = _currentUser.UserId,
            Status = "Pending",
            Message = request.Message,
            RequestedUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(row);
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.ApprovalRequested, request.Message, cancellationToken);
        return new ScenarioApprovalDto
        {
            Id = row.Id,
            Status = row.Status,
            Message = row.Message,
            RequestedByUserId = row.RequestedByUserId,
            RequestedUtc = row.RequestedUtc
        };
    }

    public async Task ShareReadOnlyAsync(ShareScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(request.ScenarioId, cancellationToken);
        await _db.AddAsync(new OptimizationScenarioShare
        {
            TenantId = _currentUser.TenantId,
            OptimizationScenarioId = scenario.Id,
            SharedByUserId = _currentUser.UserId,
            SharedWithUserId = request.SharedWithUserId,
            ReadOnly = true,
            SharedUtc = DateTime.UtcNow,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        });
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Shared, $"Shared read-only with user {request.SharedWithUserId}", cancellationToken);
    }

    public async Task MarkReviewedAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.Status = ScenarioStatus.Reviewed;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Modified, "Marked reviewed", cancellationToken);
    }

    private async Task<OptimizationScenario> RequireAsync(Guid scenarioId, CancellationToken cancellationToken) =>
        await _repo.GetByScenarioIdAsync(_currentUser.TenantId, scenarioId, false, cancellationToken)
        ?? throw new KeyNotFoundException("Scenario not found.");
}

public interface IMetricsEvolutionService
{
    Task<MetricsEvolutionDto> GetEvolutionAsync(int? academicYearId, CancellationToken cancellationToken = default);
}

/// <summary>Historical charts only — no predictions, no optimizer.</summary>
public sealed class MetricsEvolutionService : IMetricsEvolutionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public MetricsEvolutionService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<MetricsEvolutionDto> GetEvolutionAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var query = _db.SchedulingOptimizationScenarios.Where(s => s.TenantId == _currentUser.TenantId && !s.IsDeleted);
        if (academicYearId.HasValue)
            query = query.Where(s => s.AcademicYearId == academicYearId.Value);

        var scenarios = await query.OrderBy(s => s.CreatedDate).AsNoTracking().ToListAsync(cancellationToken);
        var score = scenarios.Select(s => new MetricsEvolutionPointDto
        {
            DateUtc = s.CreatedDate,
            Label = s.Name,
            Value = s.CurrentScore
        }).ToList();
        var conflicts = scenarios.Select(s => new MetricsEvolutionPointDto
        {
            DateUtc = s.CreatedDate,
            Label = s.Name,
            Value = s.ConflictCount
        }).ToList();

        var metricsQuery = _db.SchedulingOptimizationMetricSnapshots.Where(m => m.TenantId == _currentUser.TenantId && !m.IsDeleted);
        if (academicYearId.HasValue)
            metricsQuery = metricsQuery.Where(m => m.AcademicYearId == academicYearId.Value);
        var metrics = await metricsQuery.OrderBy(m => m.CapturedUtc).Take(500).AsNoTracking().ToListAsync(cancellationToken);

        MetricsEvolutionPointDto[] Series(OptimizationMetricKind kind) =>
            metrics.Where(m => m.MetricKind == kind)
                .Select(m => new MetricsEvolutionPointDto { DateUtc = m.CapturedUtc, Label = m.MetricName, Value = m.Value })
                .ToArray();

        return new MetricsEvolutionDto
        {
            ScoreEvolution = score,
            ConflictEvolution = conflicts,
            Utilization = Series(OptimizationMetricKind.FacultyUtilization),
            FacultySatisfaction = Series(OptimizationMetricKind.PreferenceSatisfaction),
            RoomUsage = Series(OptimizationMetricKind.RoomUtilization),
            Travel = Series(OptimizationMetricKind.AverageTravel),
            BreakCompliance = Series(OptimizationMetricKind.AverageBreak),
            Notes = "Historical data only — charts, no predictions, no optimization."
        };
    }
}
