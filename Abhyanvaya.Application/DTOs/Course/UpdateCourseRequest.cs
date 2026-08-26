using System.Text.Json.Serialization;

namespace Abhyanvaya.Application.DTOs.Course;

public class UpdateCourseRequest
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>AI-SCHED-CATALOG/TIMETABLE P1-3 — required catalog Department ownership.</summary>
    public int DepartmentId { get; set; }

    /// <summary>
    /// AI29.1D.24 Prompt 4B.4 — Update contract:
    /// omitted ⇒ do not modify Course.ProgramId (legacy-safe);
    /// <c>null</c> ⇒ explicit unlink; <c>N</c> ⇒ assign Program N.
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

    /// <summary>Test/helper: mark ProgramId as explicitly provided (including null).</summary>
    public void SetProgramId(int? programId)
    {
        ProgramId = programId;
    }
}
