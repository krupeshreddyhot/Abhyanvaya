using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29 — One timetable entry may map to many sections (combined classes A+B+C).
/// TimetableId is denormalized for GET /api/timetable/{id}/sections.
/// </summary>
public class TimetableSection : BaseEntity
{
    public int TimetableId { get; set; }
    public int? TimetableEntryId { get; set; }
    public int SectionId { get; set; }
}
