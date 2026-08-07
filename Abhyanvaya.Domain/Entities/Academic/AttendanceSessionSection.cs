using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29 — Optional link of an attendance session to one or more sections (combined classes).
/// Additive; does not change AttendanceSession academic context fields.
/// </summary>
public class AttendanceSessionSection : BaseEntity
{
    public Guid AttendanceSessionId { get; set; }
    public int SectionId { get; set; }
}
