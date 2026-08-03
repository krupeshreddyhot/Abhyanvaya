using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling.Conflicts;

public sealed class ConflictDetectionService : IConflictDetectionService
{
    private readonly ConflictAnalyzer _analyzer;
    private readonly IConflictDetectionRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ConflictDetectionService(
        ConflictAnalyzer analyzer,
        IConflictDetectionRepository repository,
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _analyzer = analyzer;
        _repository = repository;
        _context = context;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<ConflictAnalysisReportDto> AnalyzeAsync(RunConflictDetectionRequest request, CancellationToken cancellationToken = default)
    {
        var academicYearId = request.AcademicYearId
            ?? await ResolveAcademicYearIdAsync(request.TimetableId, cancellationToken)
            ?? throw new InvalidOperationException("Academic year is required for conflict detection.");

        var started = DateTime.UtcNow;
        var (_, bag) = await _analyzer.AnalyzeAsync(TenantId, academicYearId, request.TimetableId, request.DepartmentId, cancellationToken);
        var summary = bag.BuildSummary(0, academicYearId, request.TimetableId, request.DepartmentId, started, request.TriggerSource);

        var run = new ConflictDetectionRun
        {
            TimetableId = request.TimetableId,
            AcademicYearId = academicYearId,
            DepartmentId = request.DepartmentId,
            StartedUtc = started,
            CompletedUtc = DateTime.UtcNow,
            Status = "Completed",
            TriggerSource = request.TriggerSource,
            TotalConflicts = summary.TotalConflicts,
            FacultyCount = summary.FacultyCount,
            RoomCount = summary.RoomCount,
            StudentCount = summary.StudentCount,
            CalendarCount = summary.CalendarCount,
            CriticalCount = summary.CriticalCount,
            ErrorCount = summary.ErrorCount,
            WarningCount = summary.WarningCount,
            InformationCount = summary.InformationCount
        };

        var findings = bag.Items.Select(MapFinding).ToList();
        run = await _repository.SaveRunAsync(run, findings, cancellationToken);

        return new ConflictAnalysisReportDto
        {
            Summary = ToSummaryDto(run),
            Conflicts = bag.Items.Select(ToResultDto).ToList()
        };
    }

    public async Task<ConflictWorkspaceDto> GetWorkspaceAsync(ConflictWorkspaceQuery query, CancellationToken cancellationToken = default)
    {
        ConflictAnalysisReportDto report;
        if (query.Reanalyze || !query.UseLatestRun)
        {
            report = await AnalyzeAsync(new RunConflictDetectionRequest
            {
                TimetableId = query.TimetableId,
                AcademicYearId = query.AcademicYearId,
                DepartmentId = query.DepartmentId,
                TriggerSource = "Workspace"
            }, cancellationToken);
        }
        else
        {
            var latest = await _repository.GetLatestRunAsync(TenantId, query.TimetableId, query.AcademicYearId, cancellationToken);
            if (latest is null)
            {
                report = await AnalyzeAsync(new RunConflictDetectionRequest
                {
                    TimetableId = query.TimetableId,
                    AcademicYearId = query.AcademicYearId,
                    DepartmentId = query.DepartmentId,
                    TriggerSource = "Workspace"
                }, cancellationToken);
            }
            else
            {
                report = new ConflictAnalysisReportDto
                {
                    Summary = ToSummaryDto(latest),
                    Conflicts = latest.Findings.Select(ToResultDto).ToList()
                };
            }
        }

        var filtered = report.Conflicts.AsEnumerable();
        if (query.Category.HasValue) filtered = filtered.Where(c => c.Category == query.Category);
        if (query.Severity.HasValue) filtered = filtered.Where(c => c.Severity == query.Severity);
        if (query.DepartmentId.HasValue) filtered = filtered.Where(c => c.DepartmentId == query.DepartmentId);
        if (query.StaffId.HasValue) filtered = filtered.Where(c => c.StaffId == query.StaffId);
        if (query.RoomId.HasValue) filtered = filtered.Where(c => c.RoomId == query.RoomId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            filtered = filtered.Where(c =>
                c.Description.Contains(s, StringComparison.OrdinalIgnoreCase)
                || c.RuleName.Contains(s, StringComparison.OrdinalIgnoreCase)
                || c.RuleCode.Contains(s, StringComparison.OrdinalIgnoreCase)
                || (c.WhyOccurred?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = filtered.ToList();
        return new ConflictWorkspaceDto
        {
            Summary = report.Summary,
            Conflicts = list,
            GroupedByRule = list.GroupBy(c => c.RuleCode).ToDictionary(g => g.Key, g => g.Count()),
            GroupedByCategory = list.GroupBy(c => c.Category.ToString()).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    public async Task<ConflictDashboardDto> GetDashboardAsync(int? academicYearId, int? timetableId, CancellationToken cancellationToken = default)
    {
        var yearId = academicYearId
            ?? await ResolveAcademicYearIdAsync(timetableId, cancellationToken)
            ?? throw new InvalidOperationException("Academic year is required.");

        var latest = await _repository.GetLatestRunAsync(TenantId, timetableId, yearId, cancellationToken);
        if (latest is null)
        {
            var report = await AnalyzeAsync(new RunConflictDetectionRequest
            {
                AcademicYearId = yearId,
                TimetableId = timetableId,
                TriggerSource = "Dashboard"
            }, cancellationToken);
            latest = new ConflictDetectionRun
            {
                Id = report.Summary.RunId,
                AcademicYearId = yearId,
                TimetableId = timetableId,
                TotalConflicts = report.Summary.TotalConflicts,
                FacultyCount = report.Summary.FacultyCount,
                RoomCount = report.Summary.RoomCount,
                StudentCount = report.Summary.StudentCount,
                CalendarCount = report.Summary.CalendarCount,
                CriticalCount = report.Summary.CriticalCount,
                ErrorCount = report.Summary.ErrorCount,
                WarningCount = report.Summary.WarningCount,
                InformationCount = report.Summary.InformationCount,
                Status = report.Summary.Status,
                StartedUtc = report.Summary.StartedUtc,
                CompletedUtc = report.Summary.CompletedUtc,
                TriggerSource = report.Summary.TriggerSource
            };
        }

        var trends = (await _repository.ListRecentRunsAsync(TenantId, 14, cancellationToken))
            .OrderBy(r => r.StartedUtc)
            .Select(r => new ConflictTrendPointDto
            {
                DateUtc = r.StartedUtc,
                WarningCount = r.WarningCount,
                ErrorCount = r.ErrorCount,
                CriticalCount = r.CriticalCount,
                TotalConflicts = r.TotalConflicts
            }).ToList();

        var heatMaps = new List<HeatMapDto>
        {
            await GetFacultyHeatMapAsync(yearId, null, timetableId, cancellationToken),
            await GetRoomHeatMapAsync(yearId, null, timetableId, cancellationToken),
            await GetDepartmentHeatMapAsync(yearId, null, timetableId, cancellationToken)
        };

        return new ConflictDashboardDto
        {
            LatestSummary = ToSummaryDto(latest),
            FacultyConflicts = latest.FacultyCount,
            RoomConflicts = latest.RoomCount,
            StudentConflicts = latest.StudentCount,
            CalendarConflicts = latest.CalendarCount,
            ValidationStatus = latest.CriticalCount > 0 || latest.ErrorCount > 0 ? "Attention" : latest.TotalConflicts > 0 ? "Warnings" : "Clear",
            ConflictCategories = new Dictionary<string, int>
            {
                ["Faculty"] = latest.FacultyCount,
                ["Room"] = latest.RoomCount,
                ["Student"] = latest.StudentCount,
                ["Calendar"] = latest.CalendarCount
            },
            WarningTrends = trends,
            HeatMaps = heatMaps
        };
    }

    public Task<HeatMapDto> GetFacultyHeatMapAsync(int academicYearId, int? staffId, int? timetableId, CancellationToken cancellationToken = default)
        => BuildHeatMapAsync("Faculty", academicYearId, timetableId, e => !staffId.HasValue || e.StaffId == staffId, e => e.StaffId, cancellationToken);

    public Task<HeatMapDto> GetRoomHeatMapAsync(int academicYearId, int? roomId, int? timetableId, CancellationToken cancellationToken = default)
        => BuildHeatMapAsync("Room", academicYearId, timetableId, e => !roomId.HasValue || e.RoomId == roomId, e => e.RoomId, cancellationToken);

    public Task<HeatMapDto> GetDepartmentHeatMapAsync(int academicYearId, int? departmentId, int? timetableId, CancellationToken cancellationToken = default)
        => BuildHeatMapAsync("Department", academicYearId, timetableId, e => !departmentId.HasValue || e.DepartmentId == departmentId, e => e.DepartmentId, cancellationToken);

    private async Task<HeatMapDto> BuildHeatMapAsync(
        string kind,
        int academicYearId,
        int? timetableId,
        Func<TimetableEntry, bool> filter,
        Func<TimetableEntry, int> entitySelector,
        CancellationToken cancellationToken)
    {
        var entriesQuery = _context.SchedulingTimetableEntries.AsNoTracking().Where(e => e.TenantId == TenantId);
        if (timetableId.HasValue)
            entriesQuery = entriesQuery.Where(e => e.TimetableId == timetableId.Value);
        else
        {
            var ids = _context.SchedulingTimetables
                .Where(t => t.TenantId == TenantId && t.AcademicYearId == academicYearId)
                .Select(t => t.Id);
            entriesQuery = entriesQuery.Where(e => ids.Contains(e.TimetableId));
        }

        var entries = (await entriesQuery.ToListAsync(cancellationToken)).Where(filter).ToList();
        var slotIds = entries.Select(e => e.TimeSlotId).Distinct().ToList();
        var slots = await _context.SchedulingTimeSlots.Where(s => slotIds.Contains(s.Id)).AsNoTracking()
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var cells = entries
            .GroupBy(e => new { e.DayOfWeek, e.TimeSlotId })
            .Select(g =>
            {
                var load = g.Count();
                return new HeatMapCellDto
                {
                    DayOfWeek = g.Key.DayOfWeek,
                    TimeSlotId = g.Key.TimeSlotId,
                    TimeSlotName = slots.TryGetValue(g.Key.TimeSlotId, out var s) ? s.Name : null,
                    LoadCount = load,
                    Colour = ColourForLoad(load),
                    MaxSeverity = load >= 4 ? ConflictSeverity.Critical
                        : load == 3 ? ConflictSeverity.Error
                        : load == 2 ? ConflictSeverity.Warning
                        : ConflictSeverity.Information
                };
            })
            .OrderBy(c => c.DayOfWeek).ThenBy(c => c.TimeSlotId)
            .ToList();

        var distribution = cells.GroupBy(c => c.Colour).ToDictionary(g => g.Key, g => g.Count());
        var topEntity = entries.GroupBy(entitySelector).OrderByDescending(g => g.Count()).FirstOrDefault();

        return new HeatMapDto
        {
            Kind = kind,
            EntityId = topEntity?.Key,
            EntityName = kind,
            AcademicYearId = academicYearId,
            TimetableId = timetableId,
            Cells = cells,
            LoadDistribution = distribution
        };
    }

    private static string ColourForLoad(int load) => load switch
    {
        <= 1 => "Green",
        2 => "Yellow",
        3 => "Orange",
        _ => "Red"
    };

    private async Task<int?> ResolveAcademicYearIdAsync(int? timetableId, CancellationToken cancellationToken)
    {
        if (timetableId.HasValue)
        {
            var year = await _context.SchedulingTimetables.AsNoTracking()
                .Where(t => t.Id == timetableId.Value && t.TenantId == TenantId)
                .Select(t => (int?)t.AcademicYearId)
                .FirstOrDefaultAsync(cancellationToken);
            if (year.HasValue) return year;
        }

        return await _context.SchedulingAcademicYears.AsNoTracking()
            .Where(y => y.TenantId == TenantId && y.IsCurrent)
            .Select(y => (int?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ConflictFinding MapFinding(ConflictResult r) => new()
    {
        RuleCode = r.RuleCode,
        RuleName = r.RuleName,
        Category = r.Category,
        Severity = r.Severity,
        Description = r.Description,
        WhyOccurred = r.WhyOccurred,
        SuggestedResolution = r.Recommendation.SuggestedResolution,
        TimetableId = r.TimetableId,
        TimetableEntryId = r.TimetableEntryId,
        RelatedEntryId = r.RelatedEntryId,
        DayOfWeek = r.DayOfWeek,
        TimeSlotId = r.TimeSlotId,
        StaffId = r.StaffId,
        RoomId = r.RoomId,
        DepartmentId = r.DepartmentId,
        CourseId = r.CourseId,
        GroupId = r.GroupId,
        SemesterId = r.SemesterId,
        SubjectId = r.SubjectId,
        NavigationPath = r.Recommendation.NavigationPath
    };

    private static ConflictSummaryDto ToSummaryDto(ConflictDetectionRun run) => new()
    {
        RunId = run.Id,
        TimetableId = run.TimetableId,
        AcademicYearId = run.AcademicYearId,
        DepartmentId = run.DepartmentId,
        StartedUtc = run.StartedUtc,
        CompletedUtc = run.CompletedUtc,
        Status = run.Status,
        TriggerSource = run.TriggerSource,
        TotalConflicts = run.TotalConflicts,
        FacultyCount = run.FacultyCount,
        RoomCount = run.RoomCount,
        StudentCount = run.StudentCount,
        CalendarCount = run.CalendarCount,
        CriticalCount = run.CriticalCount,
        ErrorCount = run.ErrorCount,
        WarningCount = run.WarningCount,
        InformationCount = run.InformationCount
    };

    private static ConflictResultDto ToResultDto(ConflictResult r) => new()
    {
        RuleCode = r.RuleCode,
        RuleName = r.RuleName,
        Category = r.Category,
        Severity = r.Severity,
        Description = r.Description,
        WhyOccurred = r.WhyOccurred,
        Recommendation = new ConflictRecommendationDto
        {
            SuggestedResolution = r.Recommendation.SuggestedResolution,
            NavigationPath = r.Recommendation.NavigationPath,
            TimetableId = r.Recommendation.TimetableId,
            TimetableEntryId = r.Recommendation.TimetableEntryId,
            DayOfWeek = r.Recommendation.DayOfWeek,
            TimeSlotId = r.Recommendation.TimeSlotId
        },
        TimetableId = r.TimetableId,
        TimetableEntryId = r.TimetableEntryId,
        RelatedEntryId = r.RelatedEntryId,
        DayOfWeek = r.DayOfWeek,
        TimeSlotId = r.TimeSlotId,
        StaffId = r.StaffId,
        RoomId = r.RoomId,
        DepartmentId = r.DepartmentId,
        CourseId = r.CourseId,
        GroupId = r.GroupId,
        SemesterId = r.SemesterId,
        SubjectId = r.SubjectId
    };

    private static ConflictResultDto ToResultDto(ConflictFinding f) => new()
    {
        RuleCode = f.RuleCode,
        RuleName = f.RuleName,
        Category = f.Category,
        Severity = f.Severity,
        Description = f.Description,
        WhyOccurred = f.WhyOccurred,
        Recommendation = new ConflictRecommendationDto
        {
            SuggestedResolution = f.SuggestedResolution,
            NavigationPath = f.NavigationPath,
            TimetableId = f.TimetableId,
            TimetableEntryId = f.TimetableEntryId,
            DayOfWeek = f.DayOfWeek,
            TimeSlotId = f.TimeSlotId
        },
        TimetableId = f.TimetableId,
        TimetableEntryId = f.TimetableEntryId,
        RelatedEntryId = f.RelatedEntryId,
        DayOfWeek = f.DayOfWeek,
        TimeSlotId = f.TimeSlotId,
        StaffId = f.StaffId,
        RoomId = f.RoomId,
        DepartmentId = f.DepartmentId,
        CourseId = f.CourseId,
        GroupId = f.GroupId,
        SemesterId = f.SemesterId,
        SubjectId = f.SubjectId
    };
}
