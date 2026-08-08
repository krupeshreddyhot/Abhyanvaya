using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Academic.Observability;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>Projects timeline from SectionVersion + SectionLifecycleTransition (no duplicate event store).</summary>
public sealed class SectionTimelineService : ISectionTimelineService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicTelemetryService _telemetry;

    public SectionTimelineService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicTelemetryService telemetry)
    {
        _db = db;
        _currentUser = currentUser;
        _telemetry = telemetry;
    }

    public Task<IReadOnlyList<SectionTimelineEventDto>> GetTimelineAsync(
        int sectionId,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.SectionTimeline,
            "SectionTimeline.Get",
            ct => BuildAsync(sectionId, ct),
            cancellationToken);

    private async Task<IReadOnlyList<SectionTimelineEventDto>> BuildAsync(int sectionId, CancellationToken ct)
    {
        var exists = await _db.Sections.AsNoTracking()
            .AnyAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, ct);
        if (!exists) throw new KeyNotFoundException("Section not found.");

        var events = new List<SectionTimelineEventDto>();

        var versions = await _db.SectionVersions.AsNoTracking()
            .Where(v => v.TenantId == _currentUser.TenantId && v.SectionId == sectionId)
            .ToListAsync(ct);
        foreach (var v in versions)
        {
            events.Add(new SectionTimelineEventDto
            {
                Timestamp = v.VersionDate,
                UserId = v.ChangedBy,
                Operation = v.Operation,
                EventKind = "Version",
                Notes = v.Reason,
                ToStatus = v.Status,
                VersionNumber = v.VersionNumber,
            });
        }

        var transitions = await _db.SectionLifecycleTransitions.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.SectionId == sectionId)
            .ToListAsync(ct);
        foreach (var t in transitions)
        {
            // Prefer version projection when a lifecycle version already covers the same transition.
            var covered = versions.Any(v =>
                v.Operation == "LifecycleChange"
                && Math.Abs((v.VersionDate - t.TransitionedUtc).TotalSeconds) < 2
                && string.Equals(v.Status, t.ToStatus, StringComparison.OrdinalIgnoreCase));
            if (covered) continue;

            events.Add(new SectionTimelineEventDto
            {
                Timestamp = t.TransitionedUtc,
                UserId = t.TransitionedByUserId,
                Operation = "LifecycleChange",
                EventKind = "Lifecycle",
                Notes = t.Reason,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
            });
        }

        return events.OrderByDescending(e => e.Timestamp).ToList();
    }
}
