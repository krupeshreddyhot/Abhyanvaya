using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Infrastructure.Persistence.Repositories.Scheduling;

public sealed class CampusFacilityRepository : ICampusFacilityRepository
{
    private readonly ApplicationDbContext _context;

    public CampusFacilityRepository(ApplicationDbContext context) => _context = context;

    public Task<IReadOnlyList<Campus>> ListCampusesAsync(int tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<Campus>().AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name)
            .ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<Campus>)t.Result, cancellationToken);

    public Task<Campus?> GetCampusByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Campus>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public Task<bool> CampusCodeExistsAsync(int tenantId, string code, int? excludeId, CancellationToken cancellationToken = default) =>
        _context.Set<Campus>().AnyAsync(x => x.TenantId == tenantId && x.Code == code && (!excludeId.HasValue || x.Id != excludeId.Value), cancellationToken);

    public async Task AddCampusAsync(Campus entity, CancellationToken cancellationToken = default) =>
        await _context.Set<Campus>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<Building>> ListBuildingsAsync(int tenantId, int? campusId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Building>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (campusId.HasValue) query = query.Where(x => x.CampusId == campusId.Value);
        return query.OrderBy(x => x.Name).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<Building>)t.Result, cancellationToken);
    }

    public Task<Building?> GetBuildingByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Building>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddBuildingAsync(Building entity, CancellationToken cancellationToken = default) =>
        await _context.Set<Building>().AddAsync(entity, cancellationToken);

    public Task<IReadOnlyList<Floor>> ListFloorsAsync(int tenantId, int? buildingId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Floor>().AsNoTracking().Where(x => x.TenantId == tenantId);
        if (buildingId.HasValue) query = query.Where(x => x.BuildingId == buildingId.Value);
        return query.OrderBy(x => x.LevelNumber).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<Floor>)t.Result, cancellationToken);
    }

    public Task<Floor?> GetFloorByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Floor>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddFloorAsync(Floor entity, CancellationToken cancellationToken = default) =>
        await _context.Set<Floor>().AddAsync(entity, cancellationToken);

    public async Task<(IReadOnlyList<Room> Items, int TotalCount)> SearchRoomsAsync(
        int tenantId, string? search, RoomType? roomType, RoomStatus? status,
        int? campusId, int? buildingId, int? floorId, bool? isActive,
        string? sortBy, bool sortDescending, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Room>()
            .AsNoTracking()
            .Include(r => r.Floor!).ThenInclude(f => f.Building!).ThenInclude(b => b.Campus!)
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.Name.Contains(term) || r.Code.Contains(term));
        }
        if (roomType.HasValue) query = query.Where(r => r.RoomType == roomType.Value);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        if (floorId.HasValue) query = query.Where(r => r.FloorId == floorId.Value);
        if (buildingId.HasValue) query = query.Where(r => r.Floor!.BuildingId == buildingId.Value);
        if (campusId.HasValue) query = query.Where(r => r.Floor!.Building!.CampusId == campusId.Value);
        if (isActive.HasValue) query = query.Where(r => r.IsActive == isActive.Value);

        query = sortBy?.ToLowerInvariant() switch
        {
            "code" => sortDescending ? query.OrderByDescending(r => r.Code) : query.OrderBy(r => r.Code),
            "capacity" => sortDescending ? query.OrderByDescending(r => r.Capacity) : query.OrderBy(r => r.Capacity),
            _ => sortDescending ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Room?> GetRoomByIdAsync(int tenantId, int id, CancellationToken cancellationToken = default) =>
        _context.Set<Room>()
            .Include(r => r.Floor!).ThenInclude(f => f.Building!).ThenInclude(b => b.Campus!)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);

    public async Task AddRoomAsync(Room entity, CancellationToken cancellationToken = default) =>
        await _context.Set<Room>().AddAsync(entity, cancellationToken);
}
