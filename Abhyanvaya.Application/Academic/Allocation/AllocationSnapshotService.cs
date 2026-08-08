using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationSnapshotService : IAllocationSnapshotService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AllocationSnapshotService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<AllocationSnapshotDto> CreateAsync(
        SectionAllocationContext context,
        AllocationScopeRequest scope,
        CancellationToken cancellationToken = default)
    {
        var entity = new SectionAllocationSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            ContextVersion = context.ContextVersion,
            SchemaVersion = context.SchemaVersion,
            Checksum = context.Checksum,
            GeneratedDate = DateTime.UtcNow,
            GeneratedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            AcademicYearId = scope.AcademicYearId,
            CourseId = scope.CourseId,
            GroupId = scope.GroupId,
            SemesterId = scope.SemesterId,
            ContextJson = JsonSerializer.Serialize(context, JsonOpts),
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<AllocationSnapshotDto?> GetAsync(Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var row = await _db.SectionAllocationSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.SnapshotId == snapshotId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<AllocationSnapshotDto>> ListAsync(
        AllocationScopeRequest? scope = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.SectionAllocationSnapshots.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);
        if (scope is not null)
        {
            q = q.Where(s => s.AcademicYearId == scope.AcademicYearId
                             && s.CourseId == scope.CourseId
                             && s.GroupId == scope.GroupId
                             && s.SemesterId == scope.SemesterId);
        }
        var rows = await q.OrderByDescending(s => s.GeneratedDate).Take(50).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    private static AllocationSnapshotDto Map(SectionAllocationSnapshot s) => new()
    {
        SnapshotId = s.SnapshotId,
        ContextVersion = s.ContextVersion,
        SchemaVersion = s.SchemaVersion,
        Checksum = s.Checksum,
        GeneratedDate = s.GeneratedDate,
        GeneratedBy = s.GeneratedBy,
        AcademicYearId = s.AcademicYearId,
        CourseId = s.CourseId,
        GroupId = s.GroupId,
        SemesterId = s.SemesterId,
    };
}
