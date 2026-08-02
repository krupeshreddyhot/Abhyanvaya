using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Simulation;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Sandbox;

public interface ISandboxService
{
    Task<OptimizationWorkspaceDto> GetWorkspaceAsync(int? academicYearId, int? departmentId, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> CreateAsync(CreateScenarioRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> CreateFromOptimizationAsync(CreateOptimizationScenarioRequest request, CancellationToken cancellationToken = default);
    Task<OptimizationScenarioDetailDto> GetDetailAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> SaveAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> RenameAsync(RenameScenarioRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> DuplicateAsync(DuplicateScenarioRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> FavoriteAsync(Guid scenarioId, bool favorite, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> PinAsync(Guid scenarioId, bool pin, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> TagAsync(TagScenarioRequest request, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> ArchiveAsync(Guid scenarioId, CancellationToken cancellationToken = default);
    Task<ScenarioSummaryDto> MarkTemplateAsync(Guid scenarioId, bool isTemplate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Optimization sandbox — production-isolated scenario repository.
/// Never edits production timetables. Snapshots immutable after Saved.
/// </summary>
public sealed class SandboxService : ISandboxService
{
    private readonly IOptimizationScenarioRepository _repo;
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IOptimizationSimulationService _simulation;
    private readonly IConflictDetectionService _conflicts;
    private readonly IScenarioHistoryService _history;
    private readonly IMetricsEvolutionService _evolution;

    public SandboxService(
        IOptimizationScenarioRepository repo,
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IOptimizationSimulationService simulation,
        IConflictDetectionService conflicts,
        IScenarioHistoryService history,
        IMetricsEvolutionService evolution)
    {
        _repo = repo;
        _db = db;
        _currentUser = currentUser;
        _simulation = simulation;
        _conflicts = conflicts;
        _history = history;
        _evolution = evolution;
    }

    public async Task<OptimizationWorkspaceDto> GetWorkspaceAsync(
        int? academicYearId,
        int? departmentId,
        CancellationToken cancellationToken = default)
    {
        var list = await _repo.ListAsync(_currentUser.TenantId, academicYearId, departmentId, null, null, cancellationToken);
        var summaries = list.Select(s => MapSummary(s)).ToList();
        return new OptimizationWorkspaceDto
        {
            Scenarios = summaries,
            Favorites = summaries.Where(s => s.IsFavorite).ToList(),
            Templates = summaries.Where(s => s.IsTemplate).ToList(),
            Evolution = await _evolution.GetEvolutionAsync(academicYearId, cancellationToken)
        };
    }

    public async Task<ScenarioSummaryDto> CreateAsync(CreateScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var academicYearId = request.AcademicYearId
            ?? await ResolveAcademicYearIdAsync(request.TimetableId, cancellationToken)
            ?? throw new InvalidOperationException("Academic year is required.");

        OptimizationSimulationDto? sim = null;
        if (request.SourceSimulationId.HasValue)
            sim = await _simulation.GetAsync(request.SourceSimulationId.Value, cancellationToken);
        else if (request.CaptureFromLatestSimulation)
            sim = await _simulation.SimulateAsync(new RunOptimizationSimulationRequest
            {
                AcademicYearId = academicYearId,
                TimetableId = request.TimetableId,
                DepartmentId = request.DepartmentId,
                ScenarioName = request.Name
            }, cancellationToken);

        var workspace = await _conflicts.GetWorkspaceAsync(new ConflictWorkspaceQuery
        {
            AcademicYearId = academicYearId,
            TimetableId = request.TimetableId,
            DepartmentId = request.DepartmentId,
            UseLatestRun = true
        }, cancellationToken);

        var scenario = new OptimizationScenario
        {
            TenantId = _currentUser.TenantId,
            ScenarioId = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"Scenario {DateTime.UtcNow:yyyyMMdd-HHmm}" : request.Name.Trim(),
            Description = request.Description,
            Status = ScenarioStatus.Draft,
            OwnerUserId = _currentUser.UserId,
            AcademicYearId = academicYearId,
            DepartmentId = request.DepartmentId,
            SemesterId = request.SemesterId,
            TimetableId = request.TimetableId,
            SourceSimulationId = sim?.SimulationId ?? request.SourceSimulationId,
            Category = request.Category ?? "General",
            TagsCsv = request.TagsCsv ?? "",
            CurrentScore = sim?.CurrentScore ?? 0,
            ProjectedScore = sim?.ProjectedScore ?? 0,
            ConflictCount = sim?.CurrentConflictCount ?? workspace.Summary.TotalConflicts,
            IsImmutable = false,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        scenario = await _repo.AddAsync(scenario, cancellationToken);

        var snapshot = new OptimizationSnapshot
        {
            TenantId = _currentUser.TenantId,
            SnapshotId = Guid.NewGuid(),
            OptimizationScenarioId = scenario.Id,
            Sequence = 1,
            Label = "Initial",
            SimulationId = sim?.SimulationId,
            TimetableSummaryJson = JsonSerializer.Serialize(new
            {
                request.TimetableId,
                academicYearId,
                EntryNote = "Read-only production reference — sandbox never edits timetable."
            }),
            SimulationJson = sim is null ? "{}" : JsonSerializer.Serialize(sim),
            ScoresJson = JsonSerializer.Serialize(new { sim?.CurrentScore, sim?.ProjectedScore, sim?.BaselineScore, sim?.ProjectedScoreDetail }),
            ConflictSummaryJson = JsonSerializer.Serialize(workspace.Summary),
            MetricsJson = sim is null ? "[]" : JsonSerializer.Serialize(sim.Metrics),
            RecommendationsJson = "[]",
            CapturedUtc = DateTime.UtcNow,
            IsImmutable = true,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(snapshot);
        await _repo.SaveChangesAsync(cancellationToken);

        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Created, $"Created draft '{scenario.Name}'", cancellationToken);
        return MapSummary(scenario, snapshotCount: 1);
    }

    public async Task<ScenarioSummaryDto> CreateFromOptimizationAsync(
        CreateOptimizationScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var scenario = new OptimizationScenario
        {
            TenantId = _currentUser.TenantId,
            ScenarioId = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"Optimization {DateTime.UtcNow:yyyyMMdd-HHmm}" : request.Name.Trim(),
            Description = request.Description,
            Status = ScenarioStatus.Draft,
            OwnerUserId = _currentUser.UserId,
            AcademicYearId = request.AcademicYearId,
            DepartmentId = request.DepartmentId,
            TimetableId = request.TimetableId,
            Category = request.Category ?? "Optimization",
            TagsCsv = request.TagsCsv ?? "phase3",
            CurrentScore = request.BaselineScore,
            ProjectedScore = request.ProjectedScore,
            ConflictCount = request.ConflictCount,
            IsImmutable = false,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        scenario = await _repo.AddAsync(scenario, cancellationToken);

        var snapshot = new OptimizationSnapshot
        {
            TenantId = _currentUser.TenantId,
            SnapshotId = Guid.NewGuid(),
            OptimizationScenarioId = scenario.Id,
            Sequence = 1,
            Label = "OptimizationPipeline",
            TimetableSummaryJson = JsonSerializer.Serialize(new
            {
                request.TimetableId,
                request.AcademicYearId,
                request.RunId,
                EntryNote = "Sandbox proposal from Optimization Engine — production timetable untouched."
            }),
            SimulationJson = request.IntermediateResultsJson,
            ScoresJson = JsonSerializer.Serialize(new { request.BaselineScore, request.ProjectedScore, request.ComparisonJson }),
            ConflictSummaryJson = JsonSerializer.Serialize(new { request.ConflictCount }),
            MetricsJson = request.MetricsJson,
            RecommendationsJson = request.CandidatesJson,
            CapturedUtc = DateTime.UtcNow,
            IsImmutable = true,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        await _db.AddAsync(snapshot);
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Created,
            $"Created from optimization run {request.RunId}", cancellationToken);
        return MapSummary(scenario, snapshotCount: 1);
    }

    public async Task<OptimizationScenarioDetailDto> GetDetailAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.ViewCount += 1;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Viewed, "Viewed scenario details", cancellationToken);

        var history = await _history.ListAsync(scenario.Id, cancellationToken);
        var notes = await _db.SchedulingOptimizationScenarioNotes.Where(n => n.OptimizationScenarioId == scenario.Id && !n.IsDeleted)
            .AsNoTracking().OrderByDescending(n => n.CreatedDate)
            .Select(n => new ScenarioNoteDto { Id = n.Id, UserId = n.UserId, NoteText = n.NoteText, CreatedUtc = n.CreatedDate }).ToListAsync(cancellationToken);
        var comments = await _db.SchedulingOptimizationScenarioComments.Where(n => n.OptimizationScenarioId == scenario.Id && !n.IsDeleted)
            .AsNoTracking().OrderByDescending(n => n.CreatedDate)
            .Select(n => new ScenarioCommentDto { Id = n.Id, UserId = n.UserId, CommentText = n.CommentText, CreatedUtc = n.CreatedDate }).ToListAsync(cancellationToken);
        var bookmarks = await _db.SchedulingOptimizationScenarioBookmarks.Where(n => n.OptimizationScenarioId == scenario.Id && !n.IsDeleted)
            .AsNoTracking().Select(n => new ScenarioBookmarkDto { Id = n.Id, Name = n.Name }).ToListAsync(cancellationToken);
        var approvals = await _db.SchedulingOptimizationScenarioApprovalRequests.Where(n => n.OptimizationScenarioId == scenario.Id && !n.IsDeleted)
            .AsNoTracking().OrderByDescending(n => n.RequestedUtc)
            .Select(n => new ScenarioApprovalDto
            {
                Id = n.Id,
                Status = n.Status,
                Message = n.Message,
                RequestedByUserId = n.RequestedByUserId,
                RequestedUtc = n.RequestedUtc
            }).ToListAsync(cancellationToken);

        return new OptimizationScenarioDetailDto
        {
            Summary = MapSummary(scenario, scenario.Snapshots.Count),
            Snapshots = scenario.Snapshots.OrderBy(s => s.Sequence).Select(MapSnapshot).ToList(),
            History = history,
            Notes = notes,
            Comments = comments,
            Bookmarks = bookmarks,
            Approvals = approvals
        };
    }

    public async Task<ScenarioSummaryDto> SaveAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        if (scenario.Status == ScenarioStatus.Draft)
            scenario.Status = ScenarioStatus.Saved;
        scenario.IsImmutable = true;
        scenario.UpdatedDate = DateTime.UtcNow;
        scenario.UpdatedBy = _currentUser.UserId;
        foreach (var snap in scenario.Snapshots)
            snap.IsImmutable = true;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Modified, "Scenario saved (immutable snapshots)", cancellationToken);
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    public async Task<ScenarioSummaryDto> RenameAsync(RenameScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(request.ScenarioId, cancellationToken);
        EnsureMutableMetadata(scenario); // rename allowed even when immutable; payload not changed
        var old = scenario.Name;
        scenario.Name = request.Name.Trim();
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Renamed, $"Renamed '{old}' → '{scenario.Name}'", cancellationToken);
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    public async Task<ScenarioSummaryDto> DuplicateAsync(DuplicateScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var source = await RequireAsync(request.ScenarioId, cancellationToken);
        var copy = new OptimizationScenario
        {
            TenantId = _currentUser.TenantId,
            ScenarioId = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.NewName) ? $"{source.Name} (Copy)" : request.NewName.Trim(),
            Description = source.Description,
            Status = ScenarioStatus.Draft,
            OwnerUserId = _currentUser.UserId,
            AcademicYearId = source.AcademicYearId,
            DepartmentId = source.DepartmentId,
            SemesterId = source.SemesterId,
            TimetableId = source.TimetableId,
            SourceSimulationId = source.SourceSimulationId,
            ParentScenarioId = source.ScenarioId,
            Category = source.Category,
            TagsCsv = source.TagsCsv,
            CurrentScore = source.CurrentScore,
            ProjectedScore = source.ProjectedScore,
            ConflictCount = source.ConflictCount,
            IsImmutable = false,
            IsTemplate = false,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };
        copy = await _repo.AddAsync(copy, cancellationToken);

        var seq = 1;
        foreach (var snap in source.Snapshots.OrderBy(s => s.Sequence))
        {
            await _db.AddAsync(new OptimizationSnapshot
            {
                TenantId = _currentUser.TenantId,
                SnapshotId = Guid.NewGuid(),
                OptimizationScenarioId = copy.Id,
                Sequence = seq++,
                Label = snap.Label,
                SimulationId = snap.SimulationId,
                TimetableSummaryJson = snap.TimetableSummaryJson,
                SimulationJson = snap.SimulationJson,
                ScoresJson = snap.ScoresJson,
                ConflictSummaryJson = snap.ConflictSummaryJson,
                MetricsJson = snap.MetricsJson,
                RecommendationsJson = snap.RecommendationsJson,
                CapturedUtc = DateTime.UtcNow,
                IsImmutable = true,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            });
        }
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(source.Id, ScenarioHistoryAction.Duplicated, $"Duplicated to {copy.ScenarioId}", cancellationToken);
        await _history.RecordAsync(copy.Id, ScenarioHistoryAction.Created, $"Duplicated from {source.ScenarioId}", cancellationToken);
        return MapSummary(copy, source.Snapshots.Count);
    }

    public async Task DeleteAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Deleted, "Soft-deleted scenario", cancellationToken);
        await _repo.SoftDeleteAsync(scenario, cancellationToken);
    }

    public async Task<ScenarioSummaryDto> FavoriteAsync(Guid scenarioId, bool favorite, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.IsFavorite = favorite;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        if (favorite)
        {
            var exists = await _db.SchedulingOptimizationScenarioFavorites.AnyAsync(
                f => f.OptimizationScenarioId == scenario.Id && f.UserId == _currentUser.UserId && !f.IsDeleted, cancellationToken);
            if (!exists)
            {
                await _db.AddAsync(new OptimizationScenarioFavorite
                {
                    TenantId = _currentUser.TenantId,
                    OptimizationScenarioId = scenario.Id,
                    UserId = _currentUser.UserId,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = _currentUser.UserId
                });
                await _repo.SaveChangesAsync(cancellationToken);
            }
            await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Favorited, "Favorited", cancellationToken);
        }
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    public async Task<ScenarioSummaryDto> PinAsync(Guid scenarioId, bool pin, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.IsPinned = pin;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Pinned, pin ? "Pinned" : "Unpinned", cancellationToken);
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    public async Task<ScenarioSummaryDto> TagAsync(TagScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(request.ScenarioId, cancellationToken);
        scenario.TagsCsv = request.TagsCsv ?? "";
        if (!string.IsNullOrWhiteSpace(request.Category))
            scenario.Category = request.Category!;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Tagged, $"Tags={scenario.TagsCsv}; Category={scenario.Category}", cancellationToken);
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    public async Task<ScenarioSummaryDto> ArchiveAsync(Guid scenarioId, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.Status = ScenarioStatus.Archived;
        scenario.IsImmutable = true;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        await _history.RecordAsync(scenario.Id, ScenarioHistoryAction.Archived, "Archived", cancellationToken);
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    public async Task<ScenarioSummaryDto> MarkTemplateAsync(Guid scenarioId, bool isTemplate, CancellationToken cancellationToken = default)
    {
        var scenario = await RequireAsync(scenarioId, cancellationToken);
        scenario.IsTemplate = isTemplate;
        scenario.UpdatedDate = DateTime.UtcNow;
        await _repo.SaveChangesAsync(cancellationToken);
        return MapSummary(scenario, scenario.Snapshots.Count);
    }

    private async Task<OptimizationScenario> RequireAsync(Guid scenarioId, CancellationToken cancellationToken)
    {
        return await _repo.GetByScenarioIdAsync(_currentUser.TenantId, scenarioId, includeSnapshots: true, cancellationToken)
            ?? throw new KeyNotFoundException("Scenario not found.");
    }

    private static void EnsureMutableMetadata(OptimizationScenario _)
    {
        // Metadata (name/tags/favorite) may change; snapshot payloads never mutate after save.
    }

    private async Task<int?> ResolveAcademicYearIdAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (!timetableId.HasValue) return null;
        return await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.Id == timetableId.Value && t.TenantId == _currentUser.TenantId)
            .Select(t => (int?)t.AcademicYearId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private ScenarioSummaryDto MapSummary(OptimizationScenario s, int? snapshotCount = null) => new()
    {
        ScenarioId = s.ScenarioId,
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
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
        Tags = string.IsNullOrWhiteSpace(s.TagsCsv) ? [] : s.TagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        CurrentScore = s.CurrentScore,
        ProjectedScore = s.ProjectedScore,
        ConflictCount = s.ConflictCount,
        ReplayCount = s.ReplayCount,
        ComparisonCount = s.ComparisonCount,
        ViewCount = s.ViewCount,
        SnapshotCount = snapshotCount ?? s.Snapshots?.Count ?? 0,
        CreatedUtc = s.CreatedDate,
        LastReplayedUtc = s.LastReplayedUtc,
        ModifiesProductionTimetable = false
    };

    private static OptimizationSnapshotDto MapSnapshot(OptimizationSnapshot s) => new()
    {
        SnapshotId = s.SnapshotId,
        Sequence = s.Sequence,
        Label = s.Label,
        SimulationId = s.SimulationId,
        TimetableSummaryJson = s.TimetableSummaryJson,
        SimulationJson = s.SimulationJson,
        ScoresJson = s.ScoresJson,
        ConflictSummaryJson = s.ConflictSummaryJson,
        MetricsJson = s.MetricsJson,
        RecommendationsJson = s.RecommendationsJson,
        CapturedUtc = s.CapturedUtc,
        IsImmutable = s.IsImmutable
    };
}
