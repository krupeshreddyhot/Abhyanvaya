using Abhyanvaya.Domain.Enums.Scheduling;

namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class SubjectDeliveryTypeDto
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateSubjectDeliveryTypeRequest
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateSubjectDeliveryTypeRequest
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class UpdateSubjectDeliveryFieldsRequest
{
    public int SubjectId { get; init; }
    public int DeliveryTypeId { get; init; }
    public int? PreferredRoomFeatureId { get; init; }
    public bool RequiresAttendance { get; init; } = true;
    public int? ExpectedCapacity { get; init; }
    public RoomType? RequiresRoomType { get; init; }
}
