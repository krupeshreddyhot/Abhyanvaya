using Abhyanvaya.Domain.Enums.Scheduling;



namespace Abhyanvaya.Application.DTOs.Scheduling;



public sealed class TimeSlotTemplateDto

{

    public int Id { get; init; }

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public TimeSlotTemplateType TemplateType { get; init; }

    public bool IsDefault { get; init; }

    public int SetCount { get; init; }

    public int SlotCount { get; init; }

}



public sealed class TimeSlotTemplatePreviewDto

{

    public int Id { get; init; }

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public TimeSlotTemplateType TemplateType { get; init; }

    public bool IsDefault { get; init; }

    public IReadOnlyList<TimeSlotSetDto> Sets { get; init; } = [];

    public IReadOnlyList<TimeSlotDto> Slots { get; init; } = [];

}



public sealed class CreateTimeSlotTemplateRequest

{

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public TimeSlotTemplateType TemplateType { get; init; }

    public bool IsDefault { get; init; }

}



public sealed class UpdateTimeSlotTemplateRequest

{

    public int Id { get; init; }

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public TimeSlotTemplateType TemplateType { get; init; }

    public bool IsDefault { get; init; }

}



public sealed class CloneTimeSlotTemplateRequest

{

    public int SourceTemplateId { get; init; }

    public string Name { get; init; } = null!;

    public string? Description { get; init; }

    public TimeSlotTemplateType TemplateType { get; init; }

    public bool IsDefault { get; init; }

}

