using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using ClosedXML.Excel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public interface IVersionComparisonService
{
    Task<VersionComparisonDto> CompareAsync(CompareScheduleVersionsRequest request, CancellationToken cancellationToken = default);
    Task<byte[]> ExportExcelAsync(CompareScheduleVersionsRequest request, CancellationToken cancellationToken = default);
}

public sealed class VersionComparisonService : IVersionComparisonService
{
    private readonly IScheduleVersionRepository _versionRepository;
    private readonly IVersionComparisonRepository _comparisonRepository;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CompareScheduleVersionsRequest> _validator;

    public VersionComparisonService(
        IScheduleVersionRepository versionRepository,
        IVersionComparisonRepository comparisonRepository,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IValidator<CompareScheduleVersionsRequest> validator)
    {
        _versionRepository = versionRepository;
        _comparisonRepository = comparisonRepository;
        _context = context;
        _currentUser = currentUser;
        _validator = validator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<VersionComparisonDto> CompareAsync(CompareScheduleVersionsRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        if (request.LeftVersionId == request.RightVersionId)
            throw new DomainException("Left and right versions must be different.");

        var left = await _versionRepository.GetByIdAsync(TenantId, request.LeftVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Schedule version {request.LeftVersionId} not found.");
        var right = await _versionRepository.GetByIdAsync(TenantId, request.RightVersionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Schedule version {request.RightVersionId} not found.");

        var leftEntries = await _comparisonRepository.ListEntriesForVersionAsync(TenantId, left.Id, request.DepartmentId, cancellationToken);
        var rightEntries = await _comparisonRepository.ListEntriesForVersionAsync(TenantId, right.Id, request.DepartmentId, cancellationToken);

        var names = await LoadNamesAsync(leftEntries.Concat(rightEntries).ToList(), cancellationToken);
        var diffs = BuildDifferences(leftEntries, rightEntries, names);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim();
            diffs = diffs.Where(d =>
                (d.Summary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.SubjectName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.StaffName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.RoomName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.LeftValue?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (d.RightValue?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
        }

        if (request.KindFilter.HasValue)
            diffs = diffs.Where(d => d.Kind == request.KindFilter.Value).ToList();
        if (request.CategoryFilter.HasValue)
            diffs = diffs.Where(d => d.Category == request.CategoryFilter.Value).ToList();

        var summary = new ComparisonSummaryDto
        {
            Added = diffs.Count(d => d.Kind == VersionDifferenceKind.Added),
            Modified = diffs.Count(d => d.Kind == VersionDifferenceKind.Modified),
            Removed = diffs.Count(d => d.Kind == VersionDifferenceKind.Removed),
            FacultyChanges = diffs.Count(d => d.Category == VersionDifferenceCategory.FacultyAssignment),
            RoomChanges = diffs.Count(d => d.Category == VersionDifferenceCategory.RoomAssignment),
            SubjectChanges = diffs.Count(d => d.Category == VersionDifferenceCategory.SubjectAssignment),
            PeriodChanges = diffs.Count(d => d.Category == VersionDifferenceCategory.PeriodChange),
            TimeSlotChanges = diffs.Count(d => d.Category == VersionDifferenceCategory.TimeSlotChange),
        };

        var grouped = diffs
            .GroupBy(d => d.Category.ToString())
            .ToDictionary(g => g.Key, g => (IReadOnlyList<VersionDifferenceDto>)g.ToList());

        return new VersionComparisonDto
        {
            LeftVersionId = left.Id,
            LeftVersionName = left.VersionName,
            LeftStatus = left.Status,
            RightVersionId = right.Id,
            RightVersionName = right.VersionName,
            RightStatus = right.Status,
            Summary = summary,
            Differences = diffs,
            Grouped = grouped
        };
    }

    public async Task<byte[]> ExportExcelAsync(CompareScheduleVersionsRequest request, CancellationToken cancellationToken = default)
    {
        var result = await CompareAsync(request, cancellationToken);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Comparison");
        sheet.Cell(1, 1).Value = "Kind";
        sheet.Cell(1, 2).Value = "Category";
        sheet.Cell(1, 3).Value = "Summary";
        sheet.Cell(1, 4).Value = "Day";
        sheet.Cell(1, 5).Value = "TimeSlotId";
        sheet.Cell(1, 6).Value = "Subject";
        sheet.Cell(1, 7).Value = "Staff";
        sheet.Cell(1, 8).Value = "Room";
        sheet.Cell(1, 9).Value = "LeftValue";
        sheet.Cell(1, 10).Value = "RightValue";
        sheet.Cell(1, 11).Value = "ChangedFields";

        var row = 2;
        foreach (var d in result.Differences)
        {
            sheet.Cell(row, 1).Value = d.Kind.ToString();
            sheet.Cell(row, 2).Value = d.Category.ToString();
            sheet.Cell(row, 3).Value = d.Summary;
            sheet.Cell(row, 4).Value = d.DayOfWeek;
            sheet.Cell(row, 5).Value = d.TimeSlotId;
            sheet.Cell(row, 6).Value = d.SubjectName;
            sheet.Cell(row, 7).Value = d.StaffName;
            sheet.Cell(row, 8).Value = d.RoomName;
            sheet.Cell(row, 9).Value = d.LeftValue;
            sheet.Cell(row, 10).Value = d.RightValue;
            sheet.Cell(row, 11).Value = string.Join(", ", d.ChangedFields);
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static List<VersionDifferenceDto> BuildDifferences(
        IReadOnlyList<TimetableEntry> leftEntries,
        IReadOnlyList<TimetableEntry> rightEntries,
        NameMaps names)
    {
        var leftMap = leftEntries.GroupBy(ExactKey).ToDictionary(g => g.Key, g => g.First());
        var rightMap = rightEntries.GroupBy(ExactKey).ToDictionary(g => g.Key, g => g.First());
        var diffs = new List<VersionDifferenceDto>();

        foreach (var (key, left) in leftMap)
        {
            if (!rightMap.TryGetValue(key, out var right))
            {
                diffs.Add(ToDiff(VersionDifferenceKind.Removed, VersionDifferenceCategory.RemovedEntry, left, null, names,
                    $"Removed: {names.Subject(left.SubjectId)} on day {left.DayOfWeek} slot {left.TimeSlotId}",
                    Describe(left, names), null, ["Entry"]));
                continue;
            }

            var changed = new List<string>();
            if (left.StaffId != right.StaffId) changed.Add("StaffId");
            if (left.RoomId != right.RoomId) changed.Add("RoomId");
            if (left.SubjectId != right.SubjectId) changed.Add("SubjectId");
            if (left.DayOfWeek != right.DayOfWeek) changed.Add("DayOfWeek");
            if (left.TimeSlotId != right.TimeSlotId) changed.Add("TimeSlotId");
            if (left.SubjectAllocationId != right.SubjectAllocationId) changed.Add("SubjectAllocationId");

            if (changed.Count == 0) continue;

            var category = Classify(changed);
            diffs.Add(ToDiff(VersionDifferenceKind.Modified, category, left, right, names,
                $"Modified: {names.Subject(right.SubjectId)} ({string.Join(", ", changed)})",
                Describe(left, names), Describe(right, names), changed));
        }

        foreach (var (key, right) in rightMap)
        {
            if (leftMap.ContainsKey(key)) continue;
            diffs.Add(ToDiff(VersionDifferenceKind.Added, VersionDifferenceCategory.AddedEntry, null, right, names,
                $"Added: {names.Subject(right.SubjectId)} on day {right.DayOfWeek} slot {right.TimeSlotId}",
                null, Describe(right, names), ["Entry"]));
        }

        // Period / time-slot moves: same allocation on different day/slot
        var leftByAlloc = leftEntries.GroupBy(e => e.SubjectAllocationId).ToDictionary(g => g.Key, g => g.ToList());
        var rightByAlloc = rightEntries.GroupBy(e => e.SubjectAllocationId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var allocId in leftByAlloc.Keys.Intersect(rightByAlloc.Keys))
        {
            var l = leftByAlloc[allocId];
            var r = rightByAlloc[allocId];
            foreach (var left in l)
            {
                var exact = ExactKey(left);
                if (rightMap.ContainsKey(exact)) continue;
                var moved = r.FirstOrDefault(x => !leftMap.ContainsKey(ExactKey(x)) && (x.DayOfWeek != left.DayOfWeek || x.TimeSlotId != left.TimeSlotId));
                if (moved is null) continue;
                var fields = new List<string>();
                if (left.DayOfWeek != moved.DayOfWeek) fields.Add("DayOfWeek");
                if (left.TimeSlotId != moved.TimeSlotId) fields.Add("TimeSlotId");
                if (fields.Count == 0) continue;
                var category = fields.Contains("TimeSlotId") && !fields.Contains("DayOfWeek")
                    ? VersionDifferenceCategory.TimeSlotChange
                    : VersionDifferenceCategory.PeriodChange;
                // Avoid duplicate if already recorded as remove+add only — add explicit period/slot category rows
                if (diffs.Any(d => d.LeftEntryId == left.Id && d.RightEntryId == moved.Id)) continue;
                diffs.Add(ToDiff(VersionDifferenceKind.Modified, category, left, moved, names,
                    $"Period/slot change: {names.Subject(left.SubjectId)}",
                    Describe(left, names), Describe(moved, names), fields));
            }
        }

        return diffs.OrderBy(d => d.Category).ThenBy(d => d.DayOfWeek).ThenBy(d => d.TimeSlotId).ToList();
    }

    private static VersionDifferenceCategory Classify(IReadOnlyList<string> changed)
    {
        if (changed.Contains("StaffId")) return VersionDifferenceCategory.FacultyAssignment;
        if (changed.Contains("RoomId")) return VersionDifferenceCategory.RoomAssignment;
        if (changed.Contains("SubjectId") || changed.Contains("SubjectAllocationId")) return VersionDifferenceCategory.SubjectAssignment;
        if (changed.Contains("DayOfWeek")) return VersionDifferenceCategory.PeriodChange;
        if (changed.Contains("TimeSlotId")) return VersionDifferenceCategory.TimeSlotChange;
        return VersionDifferenceCategory.Other;
    }

    private static string ExactKey(TimetableEntry e) =>
        $"{e.DayOfWeek}|{e.TimeSlotId}|{e.SubjectAllocationId}|{e.CourseId}|{e.GroupId}|{e.SemesterId}";

    private static string Describe(TimetableEntry e, NameMaps names) =>
        $"Day={e.DayOfWeek}; Slot={e.TimeSlotId}; Subject={names.Subject(e.SubjectId)}; Staff={names.Staff(e.StaffId)}; Room={names.Room(e.RoomId)}";

    private static VersionDifferenceDto ToDiff(
        VersionDifferenceKind kind,
        VersionDifferenceCategory category,
        TimetableEntry? left,
        TimetableEntry? right,
        NameMaps names,
        string summary,
        string? leftValue,
        string? rightValue,
        IReadOnlyList<string> changedFields)
    {
        var e = right ?? left!;
        return new VersionDifferenceDto
        {
            Kind = kind,
            Category = category,
            Summary = summary,
            LeftEntryId = left?.Id,
            RightEntryId = right?.Id,
            LeftTimetableId = left?.TimetableId,
            RightTimetableId = right?.TimetableId,
            DayOfWeek = e.DayOfWeek,
            TimeSlotId = e.TimeSlotId,
            SubjectId = e.SubjectId,
            SubjectName = names.Subject(e.SubjectId),
            StaffId = e.StaffId,
            StaffName = names.Staff(e.StaffId),
            RoomId = e.RoomId,
            RoomName = names.Room(e.RoomId),
            LeftValue = leftValue,
            RightValue = rightValue,
            ChangedFields = changedFields
        };
    }

    private async Task<NameMaps> LoadNamesAsync(IReadOnlyList<TimetableEntry> entries, CancellationToken cancellationToken)
    {
        var subjectIds = entries.Select(e => e.SubjectId).Distinct().ToList();
        var staffIds = entries.Select(e => e.StaffId).Distinct().ToList();
        var roomIds = entries.Select(e => e.RoomId).Distinct().ToList();

        var subjects = await (
            from s in _context.Subjects.AsNoTracking()
            join ts in _context.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id
            where subjectIds.Contains(s.Id)
            select new { s.Id, ts.Name }).ToListAsync(cancellationToken);
        var staff = await _context.StaffMembers.AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName })
            .ToListAsync(cancellationToken);
        var rooms = await _context.SchedulingRooms.AsNoTracking()
            .Where(x => roomIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.Code + " — " + x.Name })
            .ToListAsync(cancellationToken);

        return new NameMaps(
            subjects.ToDictionary(x => x.Id, x => x.Name),
            staff.ToDictionary(x => x.Id, x => x.Name),
            rooms.ToDictionary(x => x.Id, x => x.Name));
    }

    private sealed class NameMaps(
        Dictionary<int, string> subjects,
        Dictionary<int, string> staff,
        Dictionary<int, string> rooms)
    {
        public string Subject(int id) => subjects.GetValueOrDefault(id, $"Subject {id}");
        public string Staff(int id) => staff.GetValueOrDefault(id, $"Staff {id}");
        public string Room(int id) => rooms.GetValueOrDefault(id, $"Room {id}");
    }
}
