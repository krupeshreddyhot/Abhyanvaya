using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class RoomFeatureRepository : IRoomFeatureRepository
{
    private readonly ApplicationDbContext _context;

    public RoomFeatureRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<RoomFeature>> ListFeaturesAsync(int tenantId, string? category, bool? isActive, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<RoomFeature>().AsNoTracking().Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category.ToLower() == category.ToLower());
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<RoomFeature>)t.Result, cancellationToken);
    }

    public Task<RoomFeature?> GetFeatureByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<RoomFeature>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);

    public Task<bool> FeatureCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<RoomFeature>().AnyAsync(x =>
            x.TenantId == tenantId && x.Code == code && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);

    public async Task AddFeatureAsync(RoomFeature entity, CancellationToken cancellationToken = default) =>
        await _context.Set<RoomFeature>().AddAsync(entity, cancellationToken);

    public async Task AddFeaturesAsync(IEnumerable<RoomFeature> entities, CancellationToken cancellationToken = default) =>
        await _context.Set<RoomFeature>().AddRangeAsync(entities, cancellationToken);

    public Task<IReadOnlyList<RoomFeatureAssignment>> ListAssignmentsByRoomAsync(int tenantId, int roomId, CancellationToken cancellationToken = default) =>
        _context.Set<RoomFeatureAssignment>().AsNoTracking()
            .Include(x => x.RoomFeature)
            .Where(x => x.TenantId == tenantId && x.RoomId == roomId && !x.IsDeleted)
            .OrderBy(x => x.RoomFeature!.SortOrder)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<RoomFeatureAssignment>)t.Result, cancellationToken);

    public Task<RoomFeatureAssignment?> GetAssignmentAsync(int tenantId, int roomId, int roomFeatureId, CancellationToken cancellationToken = default) =>
        _context.Set<RoomFeatureAssignment>().FirstOrDefaultAsync(x =>
            x.TenantId == tenantId && x.RoomId == roomId && x.RoomFeatureId == roomFeatureId && !x.IsDeleted,
            cancellationToken);

    public Task<bool> AssignmentExistsAsync(int tenantId, int roomId, int roomFeatureId, CancellationToken cancellationToken = default) =>
        _context.Set<RoomFeatureAssignment>().AnyAsync(x =>
            x.TenantId == tenantId && x.RoomId == roomId && x.RoomFeatureId == roomFeatureId && !x.IsDeleted,
            cancellationToken);

    public async Task AddAssignmentAsync(RoomFeatureAssignment entity, CancellationToken cancellationToken = default) =>
        await _context.Set<RoomFeatureAssignment>().AddAsync(entity, cancellationToken);

    public async Task AddAssignmentsAsync(IEnumerable<RoomFeatureAssignment> entities, CancellationToken cancellationToken = default) =>
        await _context.Set<RoomFeatureAssignment>().AddRangeAsync(entities, cancellationToken);

    public Task RemoveAssignmentAsync(RoomFeatureAssignment entity, CancellationToken cancellationToken = default)
    {
        _context.Set<RoomFeatureAssignment>().Remove(entity);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoomFeatureAssignment>> ListAssignmentsByRoomIdsAsync(int tenantId, IEnumerable<int> roomIds, CancellationToken cancellationToken = default)
    {
        var ids = roomIds.ToList();
        return _context.Set<RoomFeatureAssignment>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.RoomId) && !x.IsDeleted)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => (IReadOnlyList<RoomFeatureAssignment>)t.Result, cancellationToken);
    }
}
