namespace Abhyanvaya.Application.Academic.Architecture;

/// <summary>AI29.1A.6 — Architecture dependency validation report (no business rules).</summary>
public sealed class AcademicArchitectureReport
{
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    public bool Passed { get; init; }
    public IReadOnlyList<string> Violations { get; init; } = [];
    public IReadOnlyList<string> Checks { get; init; } = [];
}
