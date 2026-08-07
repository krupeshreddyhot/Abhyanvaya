using System.Text;
using System.Text.RegularExpressions;

namespace Abhyanvaya.Application.Academic.Architecture;

/// <summary>
/// AI29.1A.6 — Generates ADR_INDEX.md from existing ADR markdown documents when present,
/// plus a curated future-ready registry for ADR-001..ADR-022.
/// </summary>
public static partial class AdrIndexGenerator
{
    private static readonly IReadOnlyList<(string Number, string Title, string Status, string Module, string Dependencies)> Registry =
    [
        ("ADR-001", "Multi-Tenant Isolation", "Accepted", "Platform", "Constitution"),
        ("ADR-002", "Soft Delete Convention", "Accepted", "Platform", "ADR-001"),
        ("ADR-003", "Audit Fields on BaseEntity", "Accepted", "Platform", "ADR-002"),
        ("ADR-004", "JWT Authentication", "Accepted", "Security", "ADR-001"),
        ("ADR-005", "Role / Permission Model", "Accepted", "Security", "ADR-004"),
        ("ADR-006", "Attendance Capture Modes", "Accepted", "Attendance", "AI22"),
        ("ADR-007", "Attendance Session Aggregate", "Accepted", "Attendance", "ADR-006"),
        ("ADR-008", "Face Recognition Pipeline", "Accepted", "Attendance / AI", "ADR-007"),
        ("ADR-009", "Enrollment Pipeline", "Accepted", "Enrollment", "ADR-008"),
        ("ADR-010", "Artifact Storage", "Accepted", "Platform", "ADR-009"),
        ("ADR-011", "Caching Strategy (ICacheService)", "Accepted", "Platform", "ADR-001"),
        ("ADR-012", "Domain Events (in-process)", "Accepted", "Platform", "ADR-003"),
        ("ADR-013", "Repository Pattern for Scheduling", "Accepted", "Scheduling", "AI30"),
        ("ADR-014", "Timetable Governance", "Accepted", "Scheduling", "ADR-013"),
        ("ADR-015", "Conflict Detection Engine", "Accepted", "Scheduling", "ADR-014"),
        ("ADR-016", "Optimization Sandbox", "Accepted", "Scheduling", "ADR-015"),
        ("ADR-017", "Enterprise Dashboards", "Accepted", "Dashboards", "AI31"),
        ("ADR-018", "Faculty Workspace Separation", "Accepted", "Faculty", "AI31"),
        ("ADR-019", "Section Management", "Accepted", "Academic", "AI29"),
        ("ADR-020", "Program Management (optional)", "Accepted", "Academic", "AI29.1A"),
        ("ADR-021", "Master Data Ownership", "Accepted", "Catalog / Scheduling", "AI30 AC1.5"),
        ("ADR-022", "Academic Organizational Unit", "Accepted", "Academic Hierarchy", "AI29.1A.5 / ADR-020"),
    ];

    public static string GenerateMarkdown(string? docsDirectory = null)
    {
        var discovered = DiscoverAdrFiles(docsDirectory);
        var sb = new StringBuilder();
        sb.AppendLine("# ADR Index");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd} UTC (AI29.1A.6 AdrIndexGenerator)");
        sb.AppendLine();
        sb.AppendLine("| ADR Number | Title | Status | Related Module | Dependencies | Source |");
        sb.AppendLine("|------------|-------|--------|----------------|--------------|--------|");

        foreach (var row in Registry)
        {
            var source = discovered.TryGetValue(row.Number, out var path)
                ? $"`{path}`"
                : "(registry / ADL)";
            sb.AppendLine($"| {row.Number} | {row.Title} | {row.Status} | {row.Module} | {row.Dependencies} | {source} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Discovered ADR documents");
        sb.AppendLine();
        if (discovered.Count == 0)
        {
            sb.AppendLine("_No ADR-*.md files found under docs; index uses curated registry (future-ready)._");
        }
        else
        {
            foreach (var kv in discovered.OrderBy(k => k.Key))
                sb.AppendLine($"- **{kv.Key}** — `{kv.Value}`");
        }

        sb.AppendLine();
        sb.AppendLine("## Flow");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.AppendLine("  ADR001[ADR-001] --> ADR011[ADR-011]");
        sb.AppendLine("  ADR013[ADR-013] --> ADR021[ADR-021]");
        sb.AppendLine("  ADR019[ADR-019] --> ADR020[ADR-020]");
        sb.AppendLine("  ADR020 --> ADR022[ADR-022]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("_Re-run AdrIndexGenerator when new ADR markdown files are added under docs/._");
        return sb.ToString();
    }

    public static IReadOnlyDictionary<string, string> DiscoverAdrFiles(string? docsDirectory)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(docsDirectory) || !Directory.Exists(docsDirectory))
            return result;

        foreach (var file in Directory.EnumerateFiles(docsDirectory, "*ADR*.md", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var match = AdrFileRegex().Match(name);
            if (!match.Success) continue;
            var number = $"ADR-{match.Groups[1].Value.PadLeft(3, '0')}";
            var relative = file.Replace('\\', '/');
            var idx = relative.LastIndexOf("/docs/", StringComparison.OrdinalIgnoreCase);
            result[number] = idx >= 0 ? relative[(idx + 1)..] : name;
        }
        return result;
    }

    [GeneratedRegex(@"ADR[-_]?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AdrFileRegex();
}
