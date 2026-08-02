using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling.Optimization.Engine;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Optimization.Approval;

public sealed class ApproveOptimizationRequest
{
    public Guid RunId { get; set; }
    public string? NewVersionName { get; set; }
    public string? Remarks { get; set; }
}

public sealed class OptimizationApprovalResultDto
{
    public Guid RunId { get; init; }
    public int DraftScheduleVersionId { get; init; }
    public string DraftVersionName { get; init; } = "";
    public int AppliedCandidateCount { get; init; }
    public bool OverwrotePublishedTimetable => false;
    public bool ModifiedExistingDraft => false;
    public string Message { get; init; } = "";
}

public interface IOptimizationApprovalService
{
    Task<OptimizationApprovalResultDto> ApproveAsync(ApproveOptimizationRequest request, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid runId, string? reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Approval creates a NEW draft schedule version from the source, then applies candidate mutations
/// only on the cloned draft. Never overwrites published or existing draft versions.
/// </summary>
public sealed class OptimizationApprovalService : IOptimizationApprovalService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IScheduleVersionService _versions;

    public OptimizationApprovalService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IScheduleVersionService versions)
    {
        _db = db;
        _currentUser = currentUser;
        _versions = versions;
    }

    public async Task<OptimizationApprovalResultDto> ApproveAsync(
        ApproveOptimizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.SchedulingOptimizationEngineRuns
            .FirstOrDefaultAsync(r => r.TenantId == _currentUser.TenantId && r.RunId == request.RunId && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Optimization run not found.");

        if (run.Status == OptimizationEngineRunStatus.Approved)
            throw new InvalidOperationException("Optimization run was already approved.");
        if (run.Status != OptimizationEngineRunStatus.Completed)
            throw new InvalidOperationException("Only completed optimization runs can be approved.");

        var sourceVersionId = run.SourceScheduleVersionId
            ?? await ResolveVersionFromTimetableAsync(run.TimetableId, cancellationToken)
            ?? throw new InvalidOperationException("Source schedule version is required to create a new draft.");

        var versionName = string.IsNullOrWhiteSpace(request.NewVersionName)
            ? $"Optimized Draft {DateTime.UtcNow:yyyyMMdd-HHmm}"
            : request.NewVersionName.Trim();

        var draft = await _versions.DuplicateAsync(new DuplicateScheduleVersionRequest
        {
            SourceVersionId = sourceVersionId,
            VersionName = versionName,
            Remarks = request.Remarks ?? $"Approved optimization run {run.RunId}",
            CloneAllTimetables = true
        }, cancellationToken);

        var candidates = JsonSerializer.Deserialize<List<OptimizationCandidate>>(run.CandidatesJson) ?? [];
        var applied = await ApplyCandidatesToDraftAsync(sourceVersionId, draft.Id, candidates, cancellationToken);

        run.Status = OptimizationEngineRunStatus.Approved;
        run.ApprovedUtc = DateTime.UtcNow;
        run.ApprovedByUserId = _currentUser.UserId;
        run.ResultDraftScheduleVersionId = draft.Id;
        run.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new OptimizationApprovalResultDto
        {
            RunId = run.RunId,
            DraftScheduleVersionId = draft.Id,
            DraftVersionName = draft.VersionName,
            AppliedCandidateCount = applied,
            Message = $"Created new draft schedule version '{draft.VersionName}' with {applied} applied proposals. Published timetable unchanged."
        };
    }

    public async Task RejectAsync(Guid runId, string? reason, CancellationToken cancellationToken = default)
    {
        var run = await _db.SchedulingOptimizationEngineRuns
            .FirstOrDefaultAsync(r => r.TenantId == _currentUser.TenantId && r.RunId == runId && !r.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Optimization run not found.");

        if (run.Status == OptimizationEngineRunStatus.Approved)
            throw new InvalidOperationException("Approved runs cannot be rejected.");

        run.Status = OptimizationEngineRunStatus.Rejected;
        run.ErrorMessage = reason;
        run.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ApplyCandidatesToDraftAsync(
        int sourceVersionId,
        int draftVersionId,
        IReadOnlyList<OptimizationCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var sourceTimetableIds = await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.ScheduleVersionId == sourceVersionId && !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var draftTimetableIds = await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.ScheduleVersionId == draftVersionId && !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var sourceEntries = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == _currentUser.TenantId && !e.IsDeleted && sourceTimetableIds.Contains(e.TimetableId))
            .Select(e => new
            {
                e.Id,
                e.TimetableId,
                e.DayOfWeek,
                e.TimeSlotId,
                e.StaffId,
                e.RoomId,
                e.SubjectAllocationId,
                e.GroupId
            })
            .ToListAsync(cancellationToken);

        var draftEntries = await _db.SchedulingTimetableEntries
            .Where(e => e.TenantId == _currentUser.TenantId && !e.IsDeleted && draftTimetableIds.Contains(e.TimetableId))
            .ToListAsync(cancellationToken);

        // Map source entry → draft entry by allocation fingerprint prior to proposed changes.
        var draftByFingerprint = draftEntries.ToLookup(e =>
            (e.DayOfWeek, e.TimeSlotId, e.StaffId, e.RoomId, e.SubjectAllocationId, e.GroupId));

        var applied = 0;
        foreach (var candidate in candidates.Where(c => c.EntryId.HasValue))
        {
            var source = sourceEntries.FirstOrDefault(e => e.Id == candidate.EntryId!.Value);
            if (source is null) continue;

            var matches = draftByFingerprint[(
                source.DayOfWeek,
                source.TimeSlotId,
                source.StaffId,
                source.RoomId,
                source.SubjectAllocationId,
                source.GroupId)].ToList();

            var target = matches.FirstOrDefault();
            if (target is null) continue;

            if (candidate.ProposedRoomId.HasValue) target.RoomId = candidate.ProposedRoomId.Value;
            if (candidate.ProposedStaffId.HasValue) target.StaffId = candidate.ProposedStaffId.Value;
            if (candidate.ProposedTimeSlotId.HasValue) target.TimeSlotId = candidate.ProposedTimeSlotId.Value;
            if (candidate.ProposedDayOfWeek.HasValue) target.DayOfWeek = candidate.ProposedDayOfWeek.Value;
            target.UpdatedDate = DateTime.UtcNow;
            target.UpdatedBy = _currentUser.UserId;
            applied++;
        }

        if (applied > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return applied;
    }

    private async Task<int?> ResolveVersionFromTimetableAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (!timetableId.HasValue) return null;
        return await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.Id == timetableId.Value && t.TenantId == _currentUser.TenantId)
            .Select(t => t.ScheduleVersionId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
