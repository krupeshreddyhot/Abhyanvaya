using System.Globalization;
using System.Text;
using Abhyanvaya.Application.DTOs.Academic;
using ClosedXML.Excel;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionOperationalReportService : ISectionOperationalReportService
{
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionReadinessService _readiness;
    private readonly ISectionMergeService _merge;
    private readonly ISectionSplitService _split;
    private readonly ISectionLifecycleService _lifecycle;
    private readonly ISectionManagementService _sections;

    public SectionOperationalReportService(
        ISectionCapacityEngine capacity,
        ISectionReadinessService readiness,
        ISectionMergeService merge,
        ISectionSplitService split,
        ISectionLifecycleService lifecycle,
        ISectionManagementService sections)
    {
        _capacity = capacity;
        _readiness = readiness;
        _merge = merge;
        _split = split;
        _lifecycle = lifecycle;
        _sections = sections;
    }

    public async Task<byte[]> ExportAsync(string reportKind, string format, CancellationToken cancellationToken = default)
    {
        var kind = (reportKind ?? "").Trim().ToLowerInvariant();
        var fmt = (format ?? "csv").Trim().ToLowerInvariant();
        var rows = await BuildRowsAsync(kind, cancellationToken);

        return fmt switch
        {
            "xlsx" or "excel" => ToExcel(rows, kind),
            "pdf" => ToPdfPlaceholder(rows, kind),
            _ => Encoding.UTF8.GetBytes(ToCsv(rows)),
        };
    }

    private async Task<List<string[]>> BuildRowsAsync(string kind, CancellationToken ct)
    {
        return kind switch
        {
            "section-occupancy" or "occupancy" =>
            [
                ["SectionId", "Code", "Name", "Occupancy%", "Current", "Max", "Status"],
                .. (await _capacity.GetOccupancyAsync(cancellationToken: ct)).Select(r => new[]
                {
                    r.SectionId.ToString(), r.SectionCode, r.SectionName,
                    r.OccupancyPercent.ToString(CultureInfo.InvariantCulture),
                    r.CurrentStrength.ToString(), r.MaximumCapacity.ToString(), r.CapacityStatus
                })
            ],
            "section-capacity" or "capacity" =>
            [
                ["SectionId", "Code", "Max", "Min", "Recommended", "Available", "Reserved", "Waiting", "Status"],
                .. (await _capacity.GetOccupancyAsync(cancellationToken: ct)).Select(r => new[]
                {
                    r.SectionId.ToString(), r.SectionCode,
                    r.MaximumCapacity.ToString(), r.MinimumCapacity.ToString(), r.RecommendedCapacity.ToString(),
                    r.AvailableSeats.ToString(), r.ReservedSeats.ToString(), r.WaitingList.ToString(), r.CapacityStatus
                })
            ],
            "merge-history" =>
            [
                ["TransactionId", "Target", "Sources", "Effective", "Status", "Reversed"],
                .. (await _merge.GetHistoryAsync(ct)).Select(r => new[]
                {
                    r.TransactionId.ToString(), r.TargetSectionId.ToString(),
                    string.Join('|', r.SourceSectionIds), r.EffectiveDate.ToString("yyyy-MM-dd"),
                    r.Status, r.IsReversed.ToString()
                })
            ],
            "split-history" =>
            [
                ["TransactionId", "Source", "Children", "Strategy", "Effective", "Status", "Reversed"],
                .. (await _split.GetHistoryAsync(ct)).Select(r => new[]
                {
                    r.TransactionId.ToString(), r.SourceSectionId.ToString(),
                    string.Join('|', r.ChildSectionIds), r.StrategyCode,
                    r.EffectiveDate.ToString("yyyy-MM-dd"), r.Status, r.IsReversed.ToString()
                })
            ],
            "section-lifecycle" or "lifecycle" => await BuildLifecycleRowsAsync(ct),
            "readiness" or "readiness-report" =>
            [
                ["SectionId", "Code", "Overall", "Detail"],
                .. (await _readiness.GetSectionHealthAsync(ct)).Select(r => new[]
                {
                    r.SectionId.ToString(), r.SectionCode, r.OverallStatus,
                    string.Join("; ", r.Checks.Select(c => $"{c.Area}:{c.Status}"))
                })
            ],
            _ =>
            [
                ["SectionId", "Code", "Name", "Status", "Strength", "Max"],
                .. (await _sections.GetSectionsAsync(cancellationToken: ct)).Select(r => new[]
                {
                    r.Id.ToString(), r.SectionCode, r.SectionName, r.Status,
                    r.CurrentStrength.ToString(), r.MaximumStrength.ToString()
                })
            ],
        };
    }

    private async Task<List<string[]>> BuildLifecycleRowsAsync(CancellationToken ct)
    {
        var sections = await _sections.GetSectionsAsync(cancellationToken: ct);
        var rows = new List<string[]> { new[] { "SectionId", "Code", "From", "To", "Utc", "Reason" } };
        foreach (var s in sections)
        {
            var hist = await _lifecycle.GetHistoryAsync(s.Id, ct);
            foreach (var h in hist)
            {
                rows.Add([
                    s.Id.ToString(), s.SectionCode, h.FromStatus, h.ToStatus,
                    h.TransitionedUtc.ToString("O"), h.Reason ?? ""
                ]);
            }
        }
        return rows;
    }

    private static string ToCsv(List<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static byte[] ToExcel(List<string[]> rows, string sheetName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Report" : sheetName[..Math.Min(31, sheetName.Length)]);
        for (var r = 0; r < rows.Count; r++)
        for (var c = 0; c < rows[r].Length; c++)
            ws.Cell(r + 1, c + 1).Value = rows[r][c];
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>Lightweight text PDF stand-in (no PDF dependency); clients may use Excel/CSV for fidelity.</summary>
    private static byte[] ToPdfPlaceholder(List<string[]> rows, string kind)
    {
        var body = $"AI29.1B Report: {kind}\n\n" + ToCsv(rows);
        return Encoding.UTF8.GetBytes(body);
    }
}
