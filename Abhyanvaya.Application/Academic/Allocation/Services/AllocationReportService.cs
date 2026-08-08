using System.Globalization;
using System.Text;
using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic.Allocation;

public sealed class AllocationReportService : IAllocationReportService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AllocationReportService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<byte[]> ExportAsync(
        string reportKind,
        string format,
        Guid? scenarioId = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildRowsAsync((reportKind ?? "").Trim().ToLowerInvariant(), scenarioId, cancellationToken);
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        return fmt switch
        {
            "xlsx" or "excel" => ToExcel(rows, reportKind ?? "allocation"),
            "pdf" => Encoding.UTF8.GetBytes("PDF placeholder\n" + ToCsv(rows)),
            _ => Encoding.UTF8.GetBytes(ToCsv(rows)),
        };
    }

    private async Task<List<string[]>> BuildRowsAsync(string kind, Guid? scenarioId, CancellationToken ct)
    {
        AllocationScenario? scenario = null;
        if (scenarioId is Guid id)
        {
            var row = await _db.AllocationEngineScenarios.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == _currentUser.TenantId && s.ScenarioId == id, ct);
            if (row is not null)
                scenario = JsonSerializer.Deserialize<AllocationScenario>(row.ScenarioJson, JsonOpts);
        }
        else
        {
            var latest = await _db.AllocationEngineScenarios.AsNoTracking()
                .Where(s => s.TenantId == _currentUser.TenantId)
                .OrderByDescending(s => s.GeneratedAt)
                .FirstOrDefaultAsync(ct);
            if (latest is not null)
                scenario = JsonSerializer.Deserialize<AllocationScenario>(latest.ScenarioJson, JsonOpts);
        }

        scenario ??= new AllocationScenario();

        return kind switch
        {
            "capacity-distribution" or "section-utilization" =>
            [
                ["SectionId", "Code", "Assigned", "Max", "Occupancy%"],
                .. scenario.SectionSummaries.Select(s => new[]
                {
                    s.SectionId.ToString(), s.SectionCode, s.AssignedCount.ToString(),
                    s.MaximumCapacity.ToString(), s.OccupancyPercent.ToString(CultureInfo.InvariantCulture),
                }),
            ],
            "constraint-violations" =>
            [
                ["Code", "Priority", "Satisfied", "Summary", "ScoreImpact"],
                .. scenario.Constraints.Where(c => !c.Satisfied).Select(c => new[]
                {
                    c.ConstraintCode, c.Priority.ToString(), "false", c.Summary,
                    c.ScoreImpact.ToString(CultureInfo.InvariantCulture),
                }),
            ],
            "student-distribution" =>
            [
                ["StudentId", "Number", "Name", "From", "To", "Explanations"],
                .. scenario.Recommendations.Select(r => new[]
                {
                    r.StudentId.ToString(), r.StudentNumber ?? "", r.StudentName ?? "",
                    r.FromSectionCode ?? "", r.ToSectionCode, string.Join(" | ", r.Explanations),
                }),
            ],
            "allocation-audit" =>
            [
                ["ScenarioId", "SessionId", "ContextId", "Checksum", "Score", "Status", "GeneratedAt"],
                [
                    scenario.ScenarioId.ToString(), scenario.SessionId.ToString(), scenario.ContextId.ToString(),
                    scenario.ContextChecksum, scenario.Score.TotalScore.ToString(CultureInfo.InvariantCulture),
                    scenario.Status, scenario.GeneratedAt.ToString("O"),
                ],
            ],
            _ => // allocation-summary
            [
                ["Metric", "Value"],
                ["ScenarioId", scenario.ScenarioId.ToString()],
                ["Students", scenario.Recommendations.Count.ToString()],
                ["Sections", scenario.SectionSummaries.Count.ToString()],
                ["TotalScore", scenario.Score.TotalScore.ToString(CultureInfo.InvariantCulture)],
                ["CapacityUtilization", scenario.Score.CapacityUtilization.ToString(CultureInfo.InvariantCulture)],
                ["PolicyCompliance", scenario.Score.PolicyCompliance.ToString(CultureInfo.InvariantCulture)],
                ["Status", scenario.Status],
            ],
        };
    }

    private static string ToCsv(List<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));
        return sb.ToString();
        static string Escape(string v)
        {
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                return $"\"{v.Replace("\"", "\"\"")}\"";
            return v;
        }
    }

    private static byte[] ToExcel(List<string[]> rows, string sheet)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheet.Length > 31 ? sheet[..31] : sheet);
        for (var r = 0; r < rows.Count; r++)
        for (var c = 0; c < rows[r].Length; c++)
            ws.Cell(r + 1, c + 1).Value = rows[r][c];
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
