using System.Text.Json.Serialization;

namespace Abhyanvaya.Application.DTOs.Course;

public class CreateCourseRequest
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>AI-SCHED-CATALOG/TIMETABLE P1-3 — required catalog Department ownership.</summary>
    public int DepartmentId { get; set; }

    /// <summary>
    /// AI29.1D.24 Prompt 4B.4 — Create contract:
    /// <c>programId: N</c> ⇒ assign Program N; <c>null</c> or omitted ⇒ unassigned when EnablePrograms.
    /// </summary>
    public int? ProgramId
    {
        get => _programId;
        set
        {
            _programId = value;
            ProgramIdSpecified = true;
        }
    }

    [JsonIgnore]
    public bool ProgramIdSpecified { get; private set; }

    private int? _programId;
}
