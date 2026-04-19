namespace Abhyanvaya.Application.DTOs.Lookup;

public class CreateGenderRequest
{
    public string Name { get; set; } = null!;
}

public class UpdateGenderRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
