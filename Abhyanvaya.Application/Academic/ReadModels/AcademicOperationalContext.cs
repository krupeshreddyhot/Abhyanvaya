namespace Abhyanvaya.Application.Academic.ReadModels;

/// <summary>
/// AI29.1D Prompt 16 — operational selection used to build a consistent academic context breadcrumb.
/// Section and Subject are siblings under Semester in the tree; both may appear in the trail when selected.
/// </summary>
public sealed class AcademicOperationalContext
{
    public int? ProgramId { get; init; }
    public int? CourseId { get; init; }
    public int? GroupId { get; init; }
    public int? SemesterId { get; init; }
    public int? SectionId { get; init; }
    public IReadOnlyList<int>? SectionIds { get; init; }
    public int? SubjectId { get; init; }
}
