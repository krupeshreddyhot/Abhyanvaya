namespace Abhyanvaya.Application.DTOs.Lookup;

public class CreateElectiveGroupRequest
{
    public string Name { get; set; } = null!;
    public int CourseId { get; set; }
    public int SemesterId { get; set; }
    public int GroupId { get; set; }
}

public class UpdateElectiveGroupRequest
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CourseId { get; set; }
    public int SemesterId { get; set; }
    public int GroupId { get; set; }
}
