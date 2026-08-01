namespace Abhyanvaya.Application.DTOs.Scheduling;

public sealed class HolidayTypeCatalogDto
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Colour { get; init; } = null!;
    public int Priority { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CreateHolidayTypeCatalogRequest
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Colour { get; init; } = null!;
    public int Priority { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateHolidayTypeCatalogRequest
{
    public int Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Colour { get; init; } = null!;
    public int Priority { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}
