namespace Abhyanvaya.Application.DTOs.Course;

public sealed record CourseMasterRowDto(
    int Id,
    string Code,
    string Name,
    int DepartmentId,
    int? ProgramId);
