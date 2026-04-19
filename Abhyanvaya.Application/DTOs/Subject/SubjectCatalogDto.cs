using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.DTOs.Subject;

public class SubjectCatalogDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int CourseId { get; set; }
    public string CourseName { get; set; } = "";
    public int GroupId { get; set; }
    public string GroupName { get; set; } = "";
    public int SemesterId { get; set; }
    public string SemesterName { get; set; } = "";
    public bool IsElective { get; set; }
    public int? ElectiveGroupId { get; set; }
    public string? ElectiveGroupName { get; set; }
    public SubjectLanguageSlot LanguageSubjectSlot { get; set; }
    public int? TeachingLanguageId { get; set; }
    public string? TeachingLanguageName { get; set; }
}
