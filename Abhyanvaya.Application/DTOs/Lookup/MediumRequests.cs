namespace Abhyanvaya.Application.DTOs.Lookup;

public class CreateMediumRequest
{
    public string Name { get; set; } = null!;
}

public class UpdateMediumRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
