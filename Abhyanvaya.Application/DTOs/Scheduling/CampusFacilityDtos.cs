using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class CampusDto
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public string? Address { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateCampusRequest
{
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public string? Address { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateCampusRequest
{
    public int Id { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public string? Address { get; init; }
    public bool IsActive { get; init; }
}

public sealed class BuildingDto
{
    public int Id { get; init; }
    public int CampusId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public bool IsActive { get; init; }
}

public sealed class CreateBuildingRequest
{
    public int CampusId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateBuildingRequest
{
    public int Id { get; init; }
    public int CampusId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public bool IsActive { get; init; }
}

public sealed class FloorDto
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string Name { get; init; } = null!;
    public int LevelNumber { get; init; }
}

public sealed class CreateFloorRequest
{
    public int BuildingId { get; init; }
    public string Name { get; init; } = null!;
    public int LevelNumber { get; init; }
}

public sealed class UpdateFloorRequest
{
    public int Id { get; init; }
    public int BuildingId { get; init; }
    public string Name { get; init; } = null!;
    public int LevelNumber { get; init; }
}

public sealed class RoomDto
{
    public int Id { get; init; }
    public int FloorId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public RoomType RoomType { get; init; }
    public int Capacity { get; init; }
    public RoomStatus Status { get; init; }
    public RoomFeatureFlags FeatureFlags { get; init; }
    public int? DepartmentId { get; init; }
    public bool IsActive { get; init; }
    public string? CampusName { get; init; }
    public string? BuildingName { get; init; }
    public string? FloorName { get; init; }
}

public sealed class CreateRoomRequest
{
    public int FloorId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public RoomType RoomType { get; init; }
    public int Capacity { get; init; }
    public RoomStatus Status { get; init; } = RoomStatus.Available;
    public RoomFeatureFlags FeatureFlags { get; init; }
    public int? DepartmentId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateRoomRequest
{
    public int Id { get; init; }
    public int FloorId { get; init; }
    public string Name { get; init; } = null!;
    public string Code { get; init; } = null!;
    public RoomType RoomType { get; init; }
    public int Capacity { get; init; }
    public RoomStatus Status { get; init; }
    public RoomFeatureFlags FeatureFlags { get; init; }
    public int? DepartmentId { get; init; }
    public bool IsActive { get; init; }
}

public sealed class RoomSearchQuery
{
    public string? Search { get; init; }
    public RoomType? RoomType { get; init; }
    public RoomStatus? Status { get; init; }
    public int? CampusId { get; init; }
    public int? BuildingId { get; init; }
    public int? FloorId { get; init; }
    public bool? IsActive { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class PagedRoomsResult
{
    public IReadOnlyList<RoomDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
