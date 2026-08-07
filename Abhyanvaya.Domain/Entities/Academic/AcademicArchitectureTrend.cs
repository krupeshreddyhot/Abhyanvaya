using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Academic;

/// <summary>
/// AI29.1A.7 — Historical architecture guard scores (observability only; never blocks runtime).
/// </summary>
public class AcademicArchitectureTrend : BaseEntity
{
    public DateTime RecordedUtc { get; set; }
    public int Score { get; set; }
    public int DependencyViolations { get; set; }
    public int ForbiddenReferences { get; set; }
    public int LayerViolations { get; set; }
    public string? Summary { get; set; }
}
