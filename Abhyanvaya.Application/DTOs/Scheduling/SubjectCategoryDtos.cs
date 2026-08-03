using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.DTOs.Scheduling;



public sealed class SubjectCategoryDto

{

    public int Id { get; init; }

    public string Code { get; init; } = null!;

    public string Name { get; init; } = null!;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }

}



public sealed class CreateSubjectCategoryRequest

{

    public string Code { get; init; } = null!;

    public string Name { get; init; } = null!;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;

}



public sealed class UpdateSubjectCategoryRequest

{

    public int Id { get; init; }

    public string Code { get; init; } = null!;

    public string Name { get; init; } = null!;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;

}



public sealed class UpdateSubjectSchedulingCategoryRequest

{

    public int SubjectId { get; init; }

    public int SubjectCategoryId { get; init; }

    public RoomType? RequiresRoomType { get; init; }

    public int? DefaultDurationMinutes { get; init; }

    public bool RequiresLabEquipment { get; init; }

}

