using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Faculty;

public interface IFacultyDashboardService
{
    Task<FacultyTodayDto> GetTodayAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
    Task<FacultyCurrentClassWorkspaceDto> GetCurrentClassAsync(CancellationToken cancellationToken = default);
    Task<FacultyTimetableViewDto> GetTimetableAsync(string view, DateOnly? anchor = null, CancellationToken cancellationToken = default);
    Task<FacultyInsightsDto> GetInsightsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FacultyScheduleNotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Enterprise faculty operational dashboard. Aggregates existing scheduling/attendance surfaces.
/// Does not duplicate timetable logic or change attendance APIs.
/// </summary>
public sealed class FacultyDashboardService : IFacultyDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttendanceSessionResolver _resolver;

    public FacultyDashboardService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAttendanceSessionResolver resolver)
    {
        _db = db;
        _currentUser = currentUser;
        _resolver = resolver;
    }

    public async Task<FacultyTodayDto> GetTodayAsync(DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        var attendanceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var resolution = await _resolver.ResolveAsync(null, attendanceDate, cancellationToken);
        var staffId = _currentUser.StaffId > 0 ? _currentUser.StaffId : (int?)null;
        var schedule = staffId.HasValue
            ? await LoadDayClassesAsync(staffId.Value, attendanceDate, cancellationToken)
            : [];

        var current = schedule.FirstOrDefault(c => c.Status == "Current");
        var next = schedule.FirstOrDefault(c => c.Status == "Upcoming");
        if (current is null && resolution.HasTimetable)
            current = MapFromResolution(resolution, "Current");

        var summary = await BuildAttendanceSummaryAsync(staffId, attendanceDate, schedule.Count, cancellationToken);
        var aiSummary = await BuildAiSummaryAsync(staffId, attendanceDate, cancellationToken);
        var pending = await LoadPendingReviewsAsync(staffId, cancellationToken);
        var notifications = await GetNotificationsAsync(cancellationToken);

        return new FacultyTodayDto
        {
            Date = attendanceDate,
            StaffId = staffId,
            Mode = resolution.Mode,
            HasTimetable = resolution.HasTimetable,
            Message = resolution.Message,
            CurrentClass = current,
            NextClass = next,
            TodaysSchedule = schedule,
            AttendanceSummary = summary,
            AiAttendanceSummary = aiSummary,
            PendingReviews = pending,
            Notifications = notifications,
            QuickActions = BuildQuickActions(resolution.HasTimetable, current),
            GeneratedUtc = DateTime.UtcNow
        };
    }

    public async Task<FacultyCurrentClassWorkspaceDto> GetCurrentClassAsync(CancellationToken cancellationToken = default)
    {
        var today = await GetTodayAsync(null, cancellationToken);
        return new FacultyCurrentClassWorkspaceDto
        {
            CurrentClass = today.CurrentClass,
            Mode = today.Mode,
            HasTimetable = today.HasTimetable,
            Message = today.HasTimetable && today.CurrentClass is not null
                ? "Current class workspace (today's active/upcoming class only)."
                : today.Message,
            QuickActions = BuildQuickActions(today.HasTimetable, today.CurrentClass)
        };
    }

    public async Task<FacultyTimetableViewDto> GetTimetableAsync(
        string view,
        DateOnly? anchor = null,
        CancellationToken cancellationToken = default)
    {
        var staffId = _currentUser.StaffId;
        var day = anchor ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (staffId <= 0)
            return new FacultyTimetableViewDto { View = view, From = day, To = day, Classes = [] };

        var normalized = string.IsNullOrWhiteSpace(view) ? "Today" : view.Trim();
        DateOnly from;
        DateOnly to;
        switch (normalized.ToLowerInvariant())
        {
            case "week":
                from = day.AddDays(-(int)day.DayOfWeek);
                to = from.AddDays(6);
                break;
            case "month":
                from = new DateOnly(day.Year, day.Month, 1);
                to = from.AddMonths(1).AddDays(-1);
                break;
            case "agenda":
                from = day;
                to = day.AddDays(14);
                break;
            default:
                normalized = "Today";
                from = day;
                to = day;
                break;
        }

        var classes = new List<FacultyClassDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
            classes.AddRange(await LoadDayClassesAsync(staffId, d, cancellationToken));

        return new FacultyTimetableViewDto
        {
            View = normalized,
            From = from,
            To = to,
            Classes = classes
        };
    }

    public async Task<FacultyInsightsDto> GetInsightsAsync(CancellationToken cancellationToken = default)
    {
        var staffId = _currentUser.StaffId;
        if (staffId <= 0)
            return new FacultyInsightsDto();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var daySchedule = await LoadDayClassesAsync(staffId, today, cancellationToken);
        var daySummary = await BuildAttendanceSummaryAsync(staffId, today, daySchedule.Count, cancellationToken);
        var ai = await BuildAiSummaryAsync(staffId, today, cancellationToken);
        var week = await PeriodSummaryAsync(staffId, weekStart, today, cancellationToken);
        var month = await PeriodSummaryAsync(staffId, monthStart, today, cancellationToken);

        var completion = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.StaffId == staffId && s.CompletedUtc.HasValue && s.StartedUtc.HasValue)
            .OrderByDescending(s => s.CompletedUtc)
            .Take(50)
            .Select(s => new { s.StartedUtc, s.CompletedUtc })
            .ToListAsync(cancellationToken);

        double? avgMinutes = completion.Count == 0
            ? null
            : completion.Average(x => (x.CompletedUtc!.Value - x.StartedUtc!.Value).TotalMinutes);

        return new FacultyInsightsDto
        {
            AttendanceTaken = daySummary.AttendanceTaken,
            Pending = daySummary.Pending,
            Missed = daySummary.Missed,
            AverageCompletionMinutes = avgMinutes,
            AiUsage = ai.AiUsageCount,
            RecognitionAccuracy = ai.AverageRecognitionAccuracy,
            Weekly = week,
            Monthly = month
        };
    }

    public async Task<IReadOnlyList<FacultyScheduleNotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-14);
        var staffId = _currentUser.StaffId;
        var history = await _db.SchedulingTimetableChangeHistories.AsNoTracking()
            .Where(h => h.TenantId == _currentUser.TenantId && !h.IsDeleted && h.OccurredUtc >= since)
            .OrderByDescending(h => h.OccurredUtc)
            .Take(40)
            .ToListAsync(cancellationToken);

        if (staffId > 0)
        {
            var myEntryIds = await _db.SchedulingTimetableEntries.AsNoTracking()
                .Where(e => e.TenantId == _currentUser.TenantId && e.StaffId == staffId && !e.IsDeleted)
                .Select(e => e.Id)
                .Take(500)
                .ToListAsync(cancellationToken);
            var mine = history.Where(h => h.EntryId.HasValue && myEntryIds.Contains(h.EntryId.Value)).ToList();
            if (mine.Count > 0) history = mine;
        }

        return history.Select(h => new FacultyScheduleNotificationDto
        {
            NotificationId = $"chg-{h.Id}",
            Kind = MapNotificationKind(h.Operation, h.Reason),
            Title = h.Operation.ToString(),
            Message = string.IsNullOrWhiteSpace(h.Reason) ? $"Timetable {h.Operation} recorded." : h.Reason!,
            OccurredUtc = h.OccurredUtc,
            TimetableId = h.TimetableId,
            EntryId = h.EntryId
        }).ToList();
    }

    private async Task<IReadOnlyList<FacultyClassDto>> LoadDayClassesAsync(
        int staffId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var dayOfWeek = (byte)date.DayOfWeek;
        var now = DateTime.UtcNow.TimeOfDay;
        var isToday = date == DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var yearId = await _db.SchedulingAcademicYears.AsNoTracking()
            .Where(y => y.TenantId == _currentUser.TenantId && y.IsCurrent)
            .Select(y => (int?)y.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!yearId.HasValue) return [];

        var timetableIds = await _db.SchedulingTimetables.AsNoTracking()
            .Where(t => t.TenantId == _currentUser.TenantId && t.AcademicYearId == yearId.Value)
            .Where(t => t.Status == TimetableStatus.Published || t.Status == TimetableStatus.Locked)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        if (timetableIds.Count == 0) return [];

        var entries = await _db.SchedulingTimetableEntries.AsNoTracking()
            .Where(e => e.TenantId == _currentUser.TenantId && !e.IsDeleted
                        && timetableIds.Contains(e.TimetableId)
                        && e.StaffId == staffId
                        && e.DayOfWeek == dayOfWeek)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0) return [];

        var slotIds = entries.Select(e => e.TimeSlotId).Distinct().ToList();
        var slots = await _db.SchedulingTimeSlots.AsNoTracking()
            .Where(s => slotIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var roomIds = entries.Select(e => e.RoomId).Distinct().ToList();
        var roomMeta = await (
            from r in _db.SchedulingRooms.AsNoTracking()
            join f in _db.SchedulingFloors.AsNoTracking() on r.FloorId equals f.Id into fj
            from f in fj.DefaultIfEmpty()
            join b in _db.SchedulingBuildings.AsNoTracking() on f.BuildingId equals b.Id into bj
            from b in bj.DefaultIfEmpty()
            where roomIds.Contains(r.Id)
            select new { r.Id, r.Name, FloorName = f != null ? f.Name : null, BuildingName = b != null ? b.Name : null }
        ).ToDictionaryAsync(x => x.Id, cancellationToken);
        var subjectIds = entries.Select(e => e.SubjectId).Distinct().ToList();
        var subjects = await (
            from s in _db.Subjects.AsNoTracking()
            join ts in _db.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id
            where subjectIds.Contains(s.Id)
            select new { s.Id, ts.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var groupIds = entries.Select(e => e.GroupId).Distinct().ToList();
        var studentCounts = await _db.Students.AsNoTracking()
            .Where(st => st.TenantId == _currentUser.TenantId && groupIds.Contains(st.GroupId))
            .GroupBy(st => st.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId
                        && s.StaffId == staffId
                        && s.AttendanceDate >= dayStart
                        && s.AttendanceDate < dayEnd)
            .Select(s => new
            {
                s.Id,
                s.SubjectId,
                s.PeriodNumber,
                s.Status,
                s.AttendanceMethod,
                s.LowConfidenceCount
            })
            .ToListAsync(cancellationToken);

        return entries
            .Select(e =>
            {
                slots.TryGetValue(e.TimeSlotId, out var slot);
                var start = slot?.StartTime;
                var end = slot?.EndTime;
                string status;
                int? remaining = null;
                if (!isToday || start is null || end is null)
                    status = "Upcoming";
                else if (start <= now && now < end)
                {
                    status = "Current";
                    remaining = (int)Math.Max(0, (end.Value - now).TotalMinutes);
                }
                else if (end <= now)
                    status = "Completed";
                else
                    status = "Upcoming";

                var session = sessions.FirstOrDefault(s =>
                    s.SubjectId == e.SubjectId &&
                    (slot is null || !slot.PeriodNumber.HasValue || s.PeriodNumber == slot.PeriodNumber));

                var attendanceStatus = session is null
                    ? (status == "Completed" ? "Missed" : "NotStarted")
                    : session.Status is AttendanceSessionStatus.Completed or AttendanceSessionStatus.Approved
                        ? "Completed"
                        : "InProgress";

                return new FacultyClassDto
                {
                    TimetableEntryId = e.Id,
                    TimetableId = e.TimetableId,
                    Status = status,
                    DayOfWeek = e.DayOfWeek,
                    TimeSlotId = e.TimeSlotId,
                    PeriodNumber = slot?.PeriodNumber,
                    StartTime = start,
                    EndTime = end,
                    MinutesRemaining = remaining,
                    CourseId = e.CourseId,
                    GroupId = e.GroupId,
                    SemesterId = e.SemesterId,
                    SubjectId = e.SubjectId,
                    SubjectName = subjects.GetValueOrDefault(e.SubjectId),
                    RoomId = e.RoomId,
                    RoomName = roomMeta.GetValueOrDefault(e.RoomId)?.Name,
                    BuildingName = roomMeta.GetValueOrDefault(e.RoomId)?.BuildingName,
                    FloorName = roomMeta.GetValueOrDefault(e.RoomId)?.FloorName,
                    StudentCount = studentCounts.GetValueOrDefault(e.GroupId),
                    AttendanceStatus = attendanceStatus,
                    AiCaptureStatus = session is null
                        ? null
                        : session.AttendanceMethod == AttendanceMethod.AIPhoto
                            ? (session.LowConfidenceCount > 0 || session.Status == AttendanceSessionStatus.AwaitingReview
                                ? "NeedsReview"
                                : "Captured")
                            : "Manual",
                    AttendanceSessionId = session?.Id
                };
            })
            .OrderBy(c => c.StartTime ?? TimeSpan.MaxValue)
            .ToList();
    }

    private async Task<FacultyAttendanceSummaryDto> BuildAttendanceSummaryAsync(
        int? staffId,
        DateOnly date,
        int classesToday,
        CancellationToken cancellationToken)
    {
        if (!staffId.HasValue)
            return new FacultyAttendanceSummaryDto { ClassesToday = classesToday };

        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.StaffId == staffId
                        && s.AttendanceDate >= dayStart && s.AttendanceDate < dayEnd)
            .Select(s => new { s.Id, s.Status })
            .ToListAsync(cancellationToken);

        var taken = sessions.Count(s => s.Status is AttendanceSessionStatus.Completed or AttendanceSessionStatus.Approved);
        var inProgress = sessions.Count(s =>
            s.Status is AttendanceSessionStatus.Draft
                or AttendanceSessionStatus.Pending
                or AttendanceSessionStatus.Processing
                or AttendanceSessionStatus.AwaitingReview);
        var missed = Math.Max(0, classesToday - taken - inProgress);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var marks = sessionIds.Count == 0
            ? []
            : await _db.Attendances.AsNoTracking()
                .Where(a => a.TenantId == _currentUser.TenantId
                            && a.AttendanceSessionId.HasValue
                            && sessionIds.Contains(a.AttendanceSessionId.Value))
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

        return new FacultyAttendanceSummaryDto
        {
            ClassesToday = classesToday,
            AttendanceTaken = taken,
            Pending = inProgress,
            Missed = missed,
            PresentMarks = marks.Where(m => m.Status == AttendanceStatus.Present).Sum(m => m.Count),
            AbsentMarks = marks.Where(m => m.Status == AttendanceStatus.Absent).Sum(m => m.Count)
        };
    }

    private async Task<FacultyAiAttendanceSummaryDto> BuildAiSummaryAsync(
        int? staffId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (!staffId.HasValue) return new FacultyAiAttendanceSummaryDto();

        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.StaffId == staffId
                        && s.AttendanceDate >= dayStart && s.AttendanceDate < dayEnd)
            .Where(s => s.AttendanceMethod == AttendanceMethod.AIPhoto)
            .Select(s => new { s.Id, s.LowConfidenceCount, s.RecognizedCount, s.DetectedFaces, s.Status })
            .ToListAsync(cancellationToken);

        var accuracies = sessions
            .Where(s => s.DetectedFaces > 0)
            .Select(s => (decimal)s.RecognizedCount * 100m / s.DetectedFaces)
            .ToList();

        return new FacultyAiAttendanceSummaryDto
        {
            SessionsToday = sessions.Count,
            PendingReviews = sessions.Count(s => s.LowConfidenceCount > 0 || s.Status == AttendanceSessionStatus.AwaitingReview),
            AverageRecognitionAccuracy = accuracies.Count == 0 ? null : Math.Round(accuracies.Average(), 2),
            AiUsageCount = sessions.Count
        };
    }

    private async Task<IReadOnlyList<FacultyPendingReviewDto>> LoadPendingReviewsAsync(
        int? staffId,
        CancellationToken cancellationToken)
    {
        if (!staffId.HasValue) return [];
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.StaffId == staffId && s.AttendanceDate >= since)
            .Where(s => s.Status == AttendanceSessionStatus.AwaitingReview || s.LowConfidenceCount > 0)
            .OrderByDescending(s => s.CreatedUtc)
            .Take(10)
            .Select(s => new FacultyPendingReviewDto
            {
                AttendanceSessionId = s.Id,
                Label = $"Period {s.PeriodNumber} · {s.AttendanceDate:yyyy-MM-dd}",
                PendingCount = s.LowConfidenceCount,
                UpdatedUtc = s.CompletedUtc ?? s.CreatedUtc,
                ReviewPath = $"/attendance/sessions/{s.Id}/review"
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<FacultyPeriodSummaryDto> PeriodSummaryAsync(
        int staffId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var fromDt = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDt = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var sessions = await _db.AttendanceSessions.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && s.StaffId == staffId
                        && s.AttendanceDate >= fromDt && s.AttendanceDate <= toDt)
            .Select(s => new { s.Status, s.AttendanceMethod, s.RecognizedCount, s.DetectedFaces })
            .ToListAsync(cancellationToken);

        var ai = sessions.Where(s => s.AttendanceMethod == AttendanceMethod.AIPhoto).ToList();
        var acc = ai.Where(s => s.DetectedFaces > 0).Select(s => (decimal)s.RecognizedCount * 100m / s.DetectedFaces).ToList();

        return new FacultyPeriodSummaryDto
        {
            Sessions = sessions.Count,
            Completed = sessions.Count(s => s.Status is AttendanceSessionStatus.Completed or AttendanceSessionStatus.Approved),
            AiSessions = ai.Count,
            AvgAccuracy = acc.Count == 0 ? null : Math.Round(acc.Average(), 2)
        };
    }

    private static FacultyClassDto MapFromResolution(DTOs.Scheduling.AttendanceSessionResolutionDto r, string status) =>
        new()
        {
            TimetableEntryId = r.TimetableEntryId,
            TimetableId = r.TimetableId,
            Status = status,
            PeriodNumber = r.PeriodNumber,
            TimeSlotId = r.TimeSlotId,
            CourseId = r.CourseId ?? 0,
            GroupId = r.GroupId ?? 0,
            SemesterId = r.SemesterId ?? 0,
            SubjectId = r.SubjectId ?? 0,
            SubjectName = r.SubjectName,
            RoomId = r.RoomId,
            RoomName = r.RoomName,
            AttendanceStatus = "NotStarted"
        };

    private static IReadOnlyList<FacultyQuickActionDto> BuildQuickActions(bool hasTimetable, FacultyClassDto? current) =>
    [
        new() { Code = "TAKE_ATTENDANCE", Label = "Take Attendance", Path = "/attendance", Primary = true, Enabled = true, Hint = hasTimetable ? "Opens current class context" : "Manual Course→Period selection" },
        new() { Code = "AI_ATTENDANCE", Label = "AI Attendance", Path = "/attendance?ai=1", Primary = true, Enabled = true, Hint = "Launch AI capture in class context" },
        new() { Code = "TIMETABLE", Label = "Today's Timetable", Path = "/faculty?tab=timetable", Enabled = true },
        new() { Code = "STUDENTS", Label = "Student List", Path = "/attendance", Enabled = true },
        new() { Code = "ROOM", Label = "Room Details", Path = current?.RoomId is int rid ? $"/setup/scheduling/rooms?highlight={rid}" : "/setup/scheduling/rooms", Enabled = current?.RoomId is not null, Hint = current?.RoomName },
        new() { Code = "REPORT_ISSUE", Label = "Report Issue", Path = "/faculty?tab=notifications", Enabled = true },
        new() { Code = "DASHBOARD", Label = "Dashboard", Path = "/faculty", Enabled = true },
    ];

    private static string MapNotificationKind(TimetableChangeOperation op, string? reason)
    {
        var text = $"{op} {reason}".ToLowerInvariant();
        if (text.Contains("cancel")) return "Cancelled";
        if (text.Contains("substitut")) return "FacultySubstitution";
        if (text.Contains("room")) return "RoomChanged";
        if (text.Contains("holiday")) return "Holiday";
        if (text.Contains("working")) return "WorkingDayChange";
        if (op is TimetableChangeOperation.Move or TimetableChangeOperation.Update) return "Rescheduled";
        if (op == TimetableChangeOperation.Delete) return "Cancelled";
        return "ScheduleChange";
    }
}
