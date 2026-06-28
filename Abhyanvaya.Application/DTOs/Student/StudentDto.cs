namespace Abhyanvaya.Application.DTOs.Student;

/// <summary>Student read model returned by API list/detail endpoints.</summary>
public class StudentDto
{
    public int Id { get; set; }
    public string? AppraId { get; set; }
    public required string StudentNumber { get; set; }
    public required string Name { get; set; }

    public int CourseId { get; set; }
    public string CourseName { get; set; } = "";

    public int GroupId { get; set; }
    public string GroupName { get; set; } = "";

    public int SemesterId { get; set; }
    public string SemesterName { get; set; } = "";

    public int GenderId { get; set; }
    public string GenderName { get; set; } = "";

    public int MediumId { get; set; }
    public string MediumName { get; set; } = "";

    public int FirstLanguageId { get; set; }
    public string FirstLanguageName { get; set; } = "";

    public int LanguageId { get; set; }
    public string LanguageName { get; set; } = "";

    public int? Batch { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? MobileNumber { get; set; }
    public string? AlternateMobileNumber { get; set; }
    public string? Email { get; set; }
    public string? ParentMobileNumber { get; set; }
    public string? ParentAlternateMobileNumber { get; set; }
    public string? FatherName { get; set; }
    public string? MotherName { get; set; }

    public string? PhotoKey { get; set; }
    public DateTime? PhotoUploadedUtc { get; set; }
    public bool PhotoVerified { get; set; }
}
