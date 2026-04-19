namespace Abhyanvaya.Application.DTOs.Lookup;

public class CreateLanguageRequest
{
    public string Name { get; set; } = null!;
}

public class UpdateLanguageRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
}
