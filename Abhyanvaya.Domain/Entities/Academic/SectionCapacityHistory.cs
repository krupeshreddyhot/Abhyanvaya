using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>AI29.1B.5 — Append-only capacity observation history.</summary>
public class SectionCapacityHistory : BaseEntity
{
    public int SectionId { get; set; }
    public int MaximumCapacity { get; set; }
    public int MinimumCapacity { get; set; }
    public int CurrentStrength { get; set; }
    public int ReservedSeats { get; set; }
    public double OccupancyPercent { get; set; }
    public DateTime RecordedDate { get; set; }
    public string? Reason { get; set; }
    public int? RecordedBy { get; set; }
}
