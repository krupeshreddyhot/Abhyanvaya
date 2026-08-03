namespace Abhyanvaya.Application.UnitTests.Architecture;

/// <summary>
/// Result of AI30 AC1.5 Architecture Guard validation against the Master Data Ownership Matrix.
/// </summary>
public sealed class ArchitectureOwnershipReport
{
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    public string SolutionRoot { get; init; } = "";
    public bool IsCompliant { get; init; }
    public IReadOnlyList<string> CatalogOwnedMasters { get; init; } = [];
    public IReadOnlyList<OwnershipFinding> Findings { get; init; } = [];
    public IReadOnlyList<string> PassedChecks { get; init; } = [];

    public IEnumerable<OwnershipFinding> Failures => Findings.Where(f => f.Severity == OwnershipSeverity.Error);

    public string ToMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Architecture Ownership Report");
        sb.AppendLine();
        sb.AppendLine($"- Generated (UTC): {GeneratedUtc:O}");
        sb.AppendLine($"- Solution root: `{SolutionRoot}`");
        sb.AppendLine($"- Compliant: **{(IsCompliant ? "YES" : "NO")}**");
        sb.AppendLine($"- Passed checks: {PassedChecks.Count}");
        sb.AppendLine($"- Findings: {Findings.Count} (errors: {Failures.Count()})");
        sb.AppendLine();
        sb.AppendLine("## Catalog-owned masters (SSOT)");
        foreach (var m in CatalogOwnedMasters)
            sb.AppendLine($"- {m}");
        sb.AppendLine();
        if (PassedChecks.Count > 0)
        {
            sb.AppendLine("## Passed checks");
            foreach (var p in PassedChecks)
                sb.AppendLine($"- {p}");
            sb.AppendLine();
        }
        if (Findings.Count > 0)
        {
            sb.AppendLine("## Findings");
            foreach (var f in Findings)
            {
                sb.AppendLine($"- **{f.Severity}** [{f.MasterEntity}] {f.Message}");
                if (!string.IsNullOrWhiteSpace(f.Path))
                    sb.AppendLine($"  - Path: `{f.Path}`");
            }
        }
        return sb.ToString();
    }
}

public enum OwnershipSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public sealed class OwnershipFinding
{
    public required string MasterEntity { get; init; }
    public required OwnershipSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? Path { get; init; }
}
