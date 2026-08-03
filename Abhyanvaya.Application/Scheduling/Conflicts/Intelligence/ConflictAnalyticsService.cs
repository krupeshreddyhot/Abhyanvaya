using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Conflicts.Intelligence;

public interface IConflictAnalyticsService
{
    Task<ConflictAnalyticsDashboardDto> GetDashboardAsync(int? academicYearId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportExcelAsync(int? academicYearId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportPdfAsync(int? academicYearId, CancellationToken cancellationToken = default);
}

public sealed class ConflictAnalyticsService : IConflictAnalyticsService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ConflictAnalyticsService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ConflictAnalyticsDashboardDto> GetDashboardAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        var runsQuery = _db.SchedulingConflictDetectionRuns.Where(r => r.TenantId == tenantId && !r.IsDeleted);
        if (academicYearId.HasValue)
            runsQuery = runsQuery.Where(r => r.AcademicYearId == academicYearId.Value);

        var runs = await runsQuery.OrderByDescending(r => r.StartedUtc).Take(90).AsNoTracking().ToListAsync(cancellationToken);
        var runIds = runs.Select(r => r.Id).ToList();
        var findings = await _db.SchedulingConflictFindings
            .Where(f => f.TenantId == tenantId && runIds.Contains(f.ConflictDetectionRunId) && !f.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var topTypes = findings.GroupBy(f => f.RuleCode)
            .Select(g => new ConflictAnalyticsNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10).ToList();

        var facultyTrends = findings.Where(f => f.Category == ConflictCategory.Faculty && f.StaffId.HasValue)
            .GroupBy(f => f.StaffId!.Value)
            .Select(g => new ConflictAnalyticsNamedCountDto { Name = $"Staff {g.Key}", Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10).ToList();

        var roomTrends = findings.Where(f => f.Category == ConflictCategory.Room && f.RoomId.HasValue)
            .GroupBy(f => f.RoomId!.Value)
            .Select(g => new ConflictAnalyticsNamedCountDto { Name = $"Room {g.Key}", Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10).ToList();

        var deptTrends = findings.Where(f => f.DepartmentId.HasValue)
            .GroupBy(f => f.DepartmentId!.Value)
            .Select(g => new ConflictAnalyticsNamedCountDto { Name = $"Dept {g.Key}", Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10).ToList();

        var weekly = runs
            .GroupBy(r => ISOWeek(r.StartedUtc))
            .OrderBy(g => g.Key)
            .Select(g => new ConflictTrendPointDto
            {
                DateUtc = g.Min(x => x.StartedUtc),
                TotalConflicts = g.Sum(x => x.TotalConflicts),
                WarningCount = g.Sum(x => x.WarningCount),
                ErrorCount = g.Sum(x => x.ErrorCount),
                CriticalCount = g.Sum(x => x.CriticalCount)
            }).ToList();

        var monthly = runs
            .GroupBy(r => new DateTime(r.StartedUtc.Year, r.StartedUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => new ConflictTrendPointDto
            {
                DateUtc = g.Key,
                TotalConflicts = g.Sum(x => x.TotalConflicts),
                WarningCount = g.Sum(x => x.WarningCount),
                ErrorCount = g.Sum(x => x.ErrorCount),
                CriticalCount = g.Sum(x => x.CriticalCount)
            }).ToList();

        // Historical only — resolution rate approximated from run-over-run total reduction (no AI).
        var resolutionRate = 0m;
        var avgResolutionHours = 0m;
        if (runs.Count >= 2)
        {
            var ordered = runs.OrderBy(r => r.StartedUtc).ToList();
            var reductions = 0;
            var pairs = 0;
            double hours = 0;
            for (var i = 1; i < ordered.Count; i++)
            {
                pairs++;
                if (ordered[i].TotalConflicts < ordered[i - 1].TotalConflicts)
                {
                    reductions++;
                    hours += (ordered[i].StartedUtc - ordered[i - 1].StartedUtc).TotalHours;
                }
            }
            resolutionRate = pairs == 0 ? 0 : Math.Round((decimal)reductions / pairs * 100m, 1);
            avgResolutionHours = reductions == 0 ? 0 : (decimal)Math.Round(hours / reductions, 1);
        }

        return new ConflictAnalyticsDashboardDto
        {
            TopConflictTypes = topTypes,
            MostViolatedRules = topTypes,
            FacultyConflictTrends = facultyTrends,
            RoomConflictTrends = roomTrends,
            DepartmentConflictTrends = deptTrends,
            WeeklyComparison = weekly,
            MonthlyComparison = monthly,
            ConflictResolutionRatePercent = resolutionRate,
            AverageResolutionTimeHours = avgResolutionHours,
            TotalHistoricalFindings = findings.Count,
            TotalRuns = runs.Count
        };
    }

    public async Task<byte[]> ExportExcelAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var dto = await GetDashboardAsync(academicYearId, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ConflictAnalytics");
        sheet.Cell(1, 1).Value = "Section";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(1, 3).Value = "Count";
        var row = 2;
        void Add(string section, IEnumerable<ConflictAnalyticsNamedCountDto> rows)
        {
            foreach (var r in rows)
            {
                sheet.Cell(row, 1).Value = section;
                sheet.Cell(row, 2).Value = r.Name;
                sheet.Cell(row, 3).Value = r.Count;
                row++;
            }
        }
        Add("TopConflictTypes", dto.TopConflictTypes);
        Add("Faculty", dto.FacultyConflictTrends);
        Add("Room", dto.RoomConflictTrends);
        Add("Department", dto.DepartmentConflictTrends);
        sheet.Cell(row, 1).Value = "ResolutionRatePercent";
        sheet.Cell(row, 3).Value = dto.ConflictResolutionRatePercent;
        row++;
        sheet.Cell(row, 1).Value = "AverageResolutionTimeHours";
        sheet.Cell(row, 3).Value = dto.AverageResolutionTimeHours;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var dto = await GetDashboardAsync(academicYearId, cancellationToken);
        var text =
            $"Conflict Analytics (historical)\nRuns={dto.TotalRuns}\nFindings={dto.TotalHistoricalFindings}\n" +
            $"ResolutionRate={dto.ConflictResolutionRatePercent}%\nAvgResolutionHours={dto.AverageResolutionTimeHours}\n" +
            string.Join("\n", dto.TopConflictTypes.Select(t => $"{t.Name}: {t.Count}"));
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    private static string ISOWeek(DateTime utc) =>
        $"{System.Globalization.ISOWeek.GetYear(utc)}-W{System.Globalization.ISOWeek.GetWeekOfYear(utc):00}";
}
