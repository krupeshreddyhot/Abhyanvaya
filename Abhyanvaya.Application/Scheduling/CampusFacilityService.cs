using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class CampusFacilityService : ICampusFacilityService
{
    private readonly ICampusFacilityRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CampusFacilityService(
        ICampusFacilityRepository repository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<CampusDto>> ListCampusesAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListCampusesAsync(TenantId, cancellationToken);
        return items.Select(MapCampus).ToList();
    }

    public async Task<CampusDto?> GetCampusByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetCampusByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapCampus(entity);
    }

    public async Task<CampusDto> CreateCampusAsync(CreateCampusRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.CampusCodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))
            throw new DomainException($"Campus code '{request.Code}' already exists.");

        var entity = new Campus
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            Address = request.Address?.Trim(),
            IsActive = request.IsActive,
        };
        await _repository.AddCampusAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapCampus(entity);
    }

    public async Task<CampusDto> UpdateCampusAsync(UpdateCampusRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetCampusByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Campus '{request.Id}' was not found.");
        if (await _repository.CampusCodeExistsAsync(TenantId, request.Code.Trim(), request.Id, cancellationToken))
            throw new DomainException($"Campus code '{request.Code}' already exists.");

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.Address = request.Address?.Trim();
        entity.IsActive = request.IsActive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapCampus(entity);
    }

    public async Task DeleteCampusAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetCampusByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Campus '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<IReadOnlyList<BuildingDto>> ListBuildingsAsync(int? campusId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListBuildingsAsync(TenantId, campusId, cancellationToken);
        return items.Select(MapBuilding).ToList();
    }

    public async Task<BuildingDto?> GetBuildingByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetBuildingByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapBuilding(entity);
    }

    public async Task<BuildingDto> CreateBuildingAsync(CreateBuildingRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCampusExistsAsync(request.CampusId, cancellationToken);
        var entity = new Building
        {
            TenantId = TenantId,
            CampusId = request.CampusId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            IsActive = request.IsActive,
        };
        await _repository.AddBuildingAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapBuilding(entity);
    }

    public async Task<BuildingDto> UpdateBuildingAsync(UpdateBuildingRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetBuildingByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Building '{request.Id}' was not found.");
        await EnsureCampusExistsAsync(request.CampusId, cancellationToken);
        entity.CampusId = request.CampusId;
        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.IsActive = request.IsActive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapBuilding(entity);
    }

    public async Task DeleteBuildingAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetBuildingByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Building '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<IReadOnlyList<FloorDto>> ListFloorsAsync(int? buildingId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListFloorsAsync(TenantId, buildingId, cancellationToken);
        return items.Select(MapFloor).ToList();
    }

    public async Task<FloorDto?> GetFloorByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetFloorByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapFloor(entity);
    }

    public async Task<FloorDto> CreateFloorAsync(CreateFloorRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureBuildingExistsAsync(request.BuildingId, cancellationToken);
        var entity = new Floor
        {
            TenantId = TenantId,
            BuildingId = request.BuildingId,
            Name = request.Name.Trim(),
            LevelNumber = request.LevelNumber,
        };
        await _repository.AddFloorAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapFloor(entity);
    }

    public async Task<FloorDto> UpdateFloorAsync(UpdateFloorRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetFloorByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Floor '{request.Id}' was not found.");
        await EnsureBuildingExistsAsync(request.BuildingId, cancellationToken);
        entity.BuildingId = request.BuildingId;
        entity.Name = request.Name.Trim();
        entity.LevelNumber = request.LevelNumber;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapFloor(entity);
    }

    public async Task DeleteFloorAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetFloorByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Floor '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<PagedRoomsResult> SearchRoomsAsync(RoomSearchQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var (items, total) = await _repository.SearchRoomsAsync(
            TenantId, query.Search, query.RoomType, query.Status, query.CampusId, query.BuildingId, query.FloorId,
            query.IsActive, query.SortBy, query.SortDescending, (page - 1) * pageSize, pageSize, cancellationToken);
        return new PagedRoomsResult
        {
            Items = items.Select(MapRoom).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<RoomDto?> GetRoomByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetRoomByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapRoom(entity);
    }

    public async Task<RoomDto> CreateRoomAsync(CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureFloorExistsAsync(request.FloorId, cancellationToken);
        if (request.Capacity <= 0) throw new DomainException("Room capacity must be greater than zero.");

        var entity = new Room
        {
            TenantId = TenantId,
            FloorId = request.FloorId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            RoomType = request.RoomType,
            Capacity = request.Capacity,
            Status = request.Status,
            FeatureFlags = request.FeatureFlags,
            DepartmentId = request.DepartmentId,
            IsActive = request.IsActive,
        };
        await _repository.AddRoomAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapRoom(entity);
    }

    public async Task<RoomDto> UpdateRoomAsync(UpdateRoomRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetRoomByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Room '{request.Id}' was not found.");
        await EnsureFloorExistsAsync(request.FloorId, cancellationToken);
        if (request.Capacity <= 0) throw new DomainException("Room capacity must be greater than zero.");

        entity.FloorId = request.FloorId;
        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.RoomType = request.RoomType;
        entity.Capacity = request.Capacity;
        entity.Status = request.Status;
        entity.FeatureFlags = request.FeatureFlags;
        entity.DepartmentId = request.DepartmentId;
        entity.IsActive = request.IsActive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapRoom(entity);
    }

    public async Task DeleteRoomAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetRoomByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Room '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private async Task EnsureCampusExistsAsync(int campusId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingCampuses.AnyAsync(x => x.TenantId == TenantId && x.Id == campusId, cancellationToken))
            throw new KeyNotFoundException($"Campus '{campusId}' was not found.");
    }

    private async Task EnsureBuildingExistsAsync(int buildingId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingBuildings.AnyAsync(x => x.TenantId == TenantId && x.Id == buildingId, cancellationToken))
            throw new KeyNotFoundException($"Building '{buildingId}' was not found.");
    }

    private async Task EnsureFloorExistsAsync(int floorId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingFloors.AnyAsync(x => x.TenantId == TenantId && x.Id == floorId, cancellationToken))
            throw new KeyNotFoundException($"Floor '{floorId}' was not found.");
    }

    private static CampusDto MapCampus(Campus x) => new()
    {
        Id = x.Id, Name = x.Name, Code = x.Code, Address = x.Address, IsActive = x.IsActive,
    };

    private static BuildingDto MapBuilding(Building x) => new()
    {
        Id = x.Id, CampusId = x.CampusId, Name = x.Name, Code = x.Code, IsActive = x.IsActive,
    };

    private static FloorDto MapFloor(Floor x) => new()
    {
        Id = x.Id, BuildingId = x.BuildingId, Name = x.Name, LevelNumber = x.LevelNumber,
    };

    private static RoomDto MapRoom(Room x) => new()
    {
        Id = x.Id,
        FloorId = x.FloorId,
        Name = x.Name,
        Code = x.Code,
        RoomType = x.RoomType,
        Capacity = x.Capacity,
        Status = x.Status,
        FeatureFlags = x.FeatureFlags,
        DepartmentId = x.DepartmentId,
        IsActive = x.IsActive,
        CampusName = x.Floor?.Building?.Campus?.Name,
        BuildingName = x.Floor?.Building?.Name,
        FloorName = x.Floor?.Name,
    };
}
