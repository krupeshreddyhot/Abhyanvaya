namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class RoomFeatureDto
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Category { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateRoomFeatureRequest
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Category { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateRoomFeatureRequest
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Category { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class RoomFeatureAssignmentDto
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public int RoomFeatureId { get; init; }
    public string FeatureCode { get; init; } = null!;
    public string FeatureName { get; init; } = null!;
    public string FeatureCategory { get; init; } = null!;
}

public sealed class AssignRoomFeatureRequest
{
    public int RoomFeatureId { get; init; }
}

public sealed class CloneRoomFeatureAssignmentsRequest
{
    public int FromRoomId { get; init; }
    public int ToRoomId { get; init; }
}
