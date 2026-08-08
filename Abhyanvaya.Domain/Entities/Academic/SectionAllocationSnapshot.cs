using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1B.7 — Immutable persisted allocation context snapshot (simulation / audit / regression).
/// Never mutate after creation.
/// </summary>
public class SectionAllocationSnapshot : BaseEntity
{
    public Guid SnapshotId { get; set; }
    public string ContextVersion { get; set; } = "";
    public string SchemaVersion { get; set; } = "";
    public string Checksum { get; set; } = "";
    public DateTime GeneratedDate { get; set; }
    public int? GeneratedBy { get; set; }

    public int AcademicYearId { get; set; }
    public int CourseId { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }

    /// <summary>Serialized immutable SectionAllocationContext JSON.</summary>
    public string ContextJson { get; set; } = "";
}
