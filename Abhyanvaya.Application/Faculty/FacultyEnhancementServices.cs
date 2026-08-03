using System.Globalization;
using System.Text;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Domain.Enums.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Faculty;

public interface IFacultyCalendarService
{
    Task<FacultyCalendarExportDto> ExportIcsAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default);
}

public interface IFacultyTimelineService
{
    Task<FacultyTimelineDto> GetDailyTimelineAsync(DateOnly? date = null, CancellationToken cancellationToken = default);
}

public interface IClassroomNavigationService
{
    Task<ClassroomNavigationDto?> GetAsync(int roomId, int? fromRoomId = null, CancellationToken cancellationToken = default);
}

public interface IFacultyProductivityService
{
    Task<FacultyAttendanceProductivityDto> GetAttendanceProductivityAsync(CancellationToken cancellationToken = default);
    Task<FacultyProductivityDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public interface IFacultySearchService
{
    Task<FacultySearchResponseDto> SearchAsync(string query, CancellationToken cancellationToken = default);
}

public interface IFacultySmartNotificationService
{
    Task<FacultySmartNotificationsDto> GetSmartAsync(CancellationToken cancellationToken = default);
}

/// <summary>AI31.5 calendar export — ICS only, no two-way sync. Reuses timetable view data.</summary>
public sealed class FacultyCalendarService : IFacultyCalendarService
{
    private readonly IFacultyDashboardService _dashboard;

    public FacultyCalendarService(IFacultyDashboardService dashboard) => _dashboard = dashboard;

    public async Task<FacultyCalendarExportDto> ExportIcsAsync(
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to ?? start.AddDays(14);
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Abhyanvaya//FacultyWorkspace//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("X-WR-CALNAME:Faculty Timetable");

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var day = await _dashboard.GetTimetableAsync("Today", d, cancellationToken);
            foreach (var c in day.Classes.Where(x => x.StartTime.HasValue && x.EndTime.HasValue))
            {
                var uid = $"faculty-{c.TimetableEntryId}-{d:yyyyMMdd}@abhyanvaya";
                var dtStart = d.ToDateTime(TimeOnly.FromTimeSpan(c.StartTime!.Value), DateTimeKind.Local);
                var dtEnd = d.ToDateTime(TimeOnly.FromTimeSpan(c.EndTime!.Value), DateTimeKind.Local);
                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:{uid}");
                sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
                sb.AppendLine($"DTSTART:{dtStart:yyyyMMdd'T'HHmmss}");
                sb.AppendLine($"DTEND:{dtEnd:yyyyMMdd'T'HHmmss}");
                sb.AppendLine($"SUMMARY:{Escape(c.SubjectName ?? $"Subject {c.SubjectId}")}");
                sb.AppendLine($"LOCATION:{Escape(string.Join(" / ", new[] { c.RoomName, c.BuildingName }.Where(x => !string.IsNullOrWhiteSpace(x))))}");
                sb.AppendLine($"DESCRIPTION:{Escape($"Attendance: {c.AttendanceStatus}")}");
                sb.AppendLine("END:VEVENT");
            }
        }

        sb.AppendLine("END:VCALENDAR");
        return new FacultyCalendarExportDto
        {
            Content = sb.ToString(),
            FileName = $"faculty-calendar-{start:yyyyMMdd}.ics",
            OutlookSubscriptionHint = "Outlook → Add calendar → From Internet → paste the subscribe ICS URL (export-only).",
            GoogleSubscriptionHint = "Google Calendar → Other calendars → From URL → paste the subscribe ICS URL (export-only)."
        };
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}

/// <summary>Daily timeline composed from today's schedule (no duplicate timetable queries beyond GetToday).</summary>
public sealed class FacultyTimelineService : IFacultyTimelineService
{
    private readonly IFacultyDashboardService _dashboard;

    public FacultyTimelineService(IFacultyDashboardService dashboard) => _dashboard = dashboard;

    public async Task<FacultyTimelineDto> GetDailyTimelineAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var today = await _dashboard.GetTodayAsync(date, cancellationToken);
        var classes = today.TodaysSchedule.OrderBy(c => c.StartTime ?? TimeSpan.MaxValue).ToList();
        var items = new List<FacultyTimelineItemDto>();

        for (var i = 0; i < classes.Count; i++)
        {
            var c = classes[i];
            items.Add(new FacultyTimelineItemDto
            {
                Kind = "Class",
                Status = c.Status,
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                Label = c.SubjectName ?? $"Subject {c.SubjectId}",
                SubjectName = c.SubjectName,
                RoomName = c.RoomName,
                BuildingName = c.BuildingName,
                AttendanceStatus = c.AttendanceStatus,
                AiReviewPending = string.Equals(c.AiCaptureStatus, "NeedsReview", StringComparison.OrdinalIgnoreCase),
                Class = c
            });

            if (i < classes.Count - 1 &&
                c.EndTime.HasValue &&
                classes[i + 1].StartTime.HasValue &&
                classes[i + 1].StartTime > c.EndTime)
            {
                var gap = classes[i + 1].StartTime!.Value - c.EndTime.Value;
                if (gap.TotalMinutes >= 5)
                {
                    items.Add(new FacultyTimelineItemDto
                    {
                        Kind = "Break",
                        Status = "Break",
                        StartTime = c.EndTime,
                        EndTime = classes[i + 1].StartTime,
                        Label = $"Break ({(int)gap.TotalMinutes} min)",
                        AttendanceStatus = "N/A"
                    });
                }
            }
        }

        return new FacultyTimelineDto { Date = today.Date, Items = items };
    }
}

public sealed class ClassroomNavigationService : IClassroomNavigationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ClassroomNavigationService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClassroomNavigationDto?> GetAsync(
        int roomId,
        int? fromRoomId = null,
        CancellationToken cancellationToken = default)
    {
        var row = await (
            from r in _db.SchedulingRooms.AsNoTracking()
            join f in _db.SchedulingFloors.AsNoTracking() on r.FloorId equals f.Id
            join b in _db.SchedulingBuildings.AsNoTracking() on f.BuildingId equals b.Id
            join c in _db.SchedulingCampuses.AsNoTracking() on b.CampusId equals c.Id
            where r.TenantId == _currentUser.TenantId && r.Id == roomId && !r.IsDeleted
            select new
            {
                r.Id,
                r.Name,
                r.Code,
                r.Capacity,
                r.RoomType,
                r.FeatureFlags,
                FloorName = f.Name,
                FloorLevel = f.LevelNumber,
                BuildingName = b.Name,
                BuildingId = b.Id,
                CampusName = c.Name
            }).FirstOrDefaultAsync(cancellationToken);

        if (row is null) return null;

        int? walk = null;
        if (fromRoomId is > 0)
        {
            var from = await (
                from r in _db.SchedulingRooms.AsNoTracking()
                join f in _db.SchedulingFloors.AsNoTracking() on r.FloorId equals f.Id
                where r.Id == fromRoomId.Value
                select new { f.BuildingId, f.LevelNumber }).FirstOrDefaultAsync(cancellationToken);
            if (from is not null)
            {
                var floorDelta = Math.Abs(from.LevelNumber - row.FloorLevel);
                walk = from.BuildingId == row.BuildingId
                    ? 2 + floorDelta * 2
                    : 8 + floorDelta * 2;
            }
        }

        var features = Enum.GetValues<RoomFeatureFlags>()
            .Where(f => f != RoomFeatureFlags.None && row.FeatureFlags.HasFlag(f))
            .Select(f => f.ToString())
            .ToList();

        return new ClassroomNavigationDto
        {
            RoomId = row.Id,
            RoomName = row.Name,
            RoomCode = row.Code,
            Capacity = row.Capacity,
            RoomType = row.RoomType.ToString(),
            Features = features,
            AccessibilityFriendly = features.Count > 0, // feature presence proxy; no GIS
            CampusName = row.CampusName,
            BuildingName = row.BuildingName,
            FloorName = row.FloorName,
            FloorLevel = row.FloorLevel,
            WalkingEstimateMinutes = walk
        };
    }
}

public sealed class FacultyProductivityService : IFacultyProductivityService
{
    private readonly IFacultyDashboardService _dashboard;

    public FacultyProductivityService(IFacultyDashboardService dashboard) => _dashboard = dashboard;

    public async Task<FacultyAttendanceProductivityDto> GetAttendanceProductivityAsync(CancellationToken cancellationToken = default)
    {
        var today = await _dashboard.GetTodayAsync(null, cancellationToken);
        var s = today.AttendanceSummary;
        var remaining = today.TodaysSchedule.Count(c =>
            (c.Status is "Current" or "Upcoming") && c.AttendanceStatus != "Completed");
        var completion = s.ClassesToday == 0
            ? 0
            : Math.Round((decimal)s.AttendanceTaken * 100m / s.ClassesToday, 1);
        var late = today.TodaysSchedule.Count(c =>
            c.Status == "Completed" && c.AttendanceStatus == "InProgress");

        var resume = today.CurrentClass is not null
            ? "/attendance"
            : today.PendingReviews.FirstOrDefault()?.ReviewPath ?? "/attendance";

        return new FacultyAttendanceProductivityDto
        {
            PendingAttendance = s.Pending,
            RemainingClasses = remaining,
            AttendanceCompletionPercent = completion,
            AiPendingReviews = today.AiAttendanceSummary.PendingReviews,
            MissedAttendance = s.Missed,
            LateAttendance = late,
            QuickResumePath = resume
        };
    }

    public async Task<FacultyProductivityDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var today = await _dashboard.GetTodayAsync(null, cancellationToken);
        var insights = await _dashboard.GetInsightsAsync(cancellationToken);
        var week = await _dashboard.GetTimetableAsync("Week", null, cancellationToken);

        var weekly = week.Classes
            .GroupBy(c => c.DayOfWeek)
            .OrderBy(g => g.Key)
            .Select(g => new FacultyChartPointDto
            {
                Label = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames[g.Key % 7],
                Value = g.Count()
            }).ToList();

        var monthly = new List<FacultyChartPointDto>
        {
            new() { Label = "Sessions", Value = insights.Monthly.Sessions },
            new() { Label = "Completed", Value = insights.Monthly.Completed },
            new() { Label = "AI", Value = insights.Monthly.AiSessions }
        };

        var roomUtil = today.TodaysSchedule
            .Where(c => !string.IsNullOrWhiteSpace(c.RoomName))
            .GroupBy(c => c.RoomName!)
            .Select(g => new FacultyChartPointDto { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToList();

        var rate = today.AttendanceSummary.ClassesToday == 0
            ? 0
            : Math.Round((decimal)today.AttendanceSummary.AttendanceTaken * 100m / today.AttendanceSummary.ClassesToday, 1);

        return new FacultyProductivityDashboardDto
        {
            ClassesToday = today.AttendanceSummary.ClassesToday,
            AttendanceCompleted = today.AttendanceSummary.AttendanceTaken,
            AttendanceRate = rate,
            AiUsage = insights.AiUsage,
            RecognitionAccuracy = insights.RecognitionAccuracy,
            WeeklyWorkload = weekly,
            MonthlyWorkload = monthly,
            RoomUtilization = roomUtil
        };
    }
}

public sealed class FacultySearchService : IFacultySearchService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FacultySearchService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<FacultySearchResponseDto> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var q = (query ?? "").Trim();
        if (q.Length < 2)
            return new FacultySearchResponseDto { Query = q, Results = [] };

        var like = q.ToLowerInvariant();
        var results = new List<FacultySearchResultDto>();
        var tenantId = _currentUser.TenantId;

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted &&
                        (s.Name.ToLower().Contains(like) || s.StudentNumber.ToLower().Contains(like)))
            .Take(8)
            .Select(s => new FacultySearchResultDto
            {
                Category = "Student",
                Title = s.Name,
                Subtitle = s.StudentNumber,
                NavigationPath = "/students",
                EntityKey = s.Id.ToString()
            }).ToListAsync(cancellationToken);
        results.AddRange(students);

        var subjects = await (
            from s in _db.Subjects.AsNoTracking()
            join ts in _db.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id
            where ts.TenantId == tenantId && ts.Name.ToLower().Contains(like)
            select new FacultySearchResultDto
            {
                Category = "Subject",
                Title = ts.Name,
                Subtitle = ts.Code ?? "",
                NavigationPath = "/faculty?tab=timetable",
                EntityKey = s.Id.ToString()
            }).Take(8).ToListAsync(cancellationToken);
        results.AddRange(subjects);

        var rooms = await _db.SchedulingRooms.AsNoTracking()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted &&
                        (r.Name.ToLower().Contains(like) || r.Code.ToLower().Contains(like)))
            .Take(8)
            .Select(r => new FacultySearchResultDto
            {
                Category = "Room",
                Title = r.Name,
                Subtitle = r.Code,
                NavigationPath = $"/faculty?tab=class&roomId={r.Id}",
                EntityKey = r.Id.ToString()
            }).ToListAsync(cancellationToken);
        results.AddRange(rooms);

        var courses = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted && c.Name.ToLower().Contains(like))
            .Take(5)
            .Select(c => new FacultySearchResultDto
            {
                Category = "Course",
                Title = c.Name,
                Subtitle = c.Code,
                NavigationPath = "/attendance",
                EntityKey = c.Id.ToString()
            }).ToListAsync(cancellationToken);
        results.AddRange(courses);

        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted && g.Name.ToLower().Contains(like))
            .Take(5)
            .Select(g => new FacultySearchResultDto
            {
                Category = "Group",
                Title = g.Name,
                Subtitle = "",
                NavigationPath = "/attendance",
                EntityKey = g.Id.ToString()
            }).ToListAsync(cancellationToken);
        results.AddRange(groups);

        var semesters = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.Name.ToLower().Contains(like))
            .Take(5)
            .Select(s => new FacultySearchResultDto
            {
                Category = "Semester",
                Title = s.Name,
                Subtitle = "",
                NavigationPath = "/attendance",
                EntityKey = s.Id.ToString()
            }).ToListAsync(cancellationToken);
        results.AddRange(semesters);

        if (_currentUser.StaffId > 0)
        {
            var sessions = await _db.AttendanceSessions.AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.StaffId == _currentUser.StaffId)
                .OrderByDescending(s => s.CreatedUtc)
                .Take(30)
                .Select(s => new { s.Id, s.PeriodNumber, s.AttendanceDate, s.Status })
                .ToListAsync(cancellationToken);
            results.AddRange(sessions
                .Where(s => $"period {s.PeriodNumber}".Contains(like) || s.Id.ToString().Contains(like, StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(s => new FacultySearchResultDto
                {
                    Category = "Attendance Session",
                    Title = $"Period {s.PeriodNumber} · {s.AttendanceDate:yyyy-MM-dd}",
                    Subtitle = s.Status.ToString(),
                    NavigationPath = $"/attendance/sessions/{s.Id}/review",
                    EntityKey = s.Id.ToString()
                }));

            results.Add(new FacultySearchResultDto
            {
                Category = "Timetable",
                Title = "My timetable",
                Subtitle = q,
                NavigationPath = "/faculty?tab=timetable",
                EntityKey = "timetable"
            });
        }

        return new FacultySearchResponseDto { Query = q, Results = results.Take(40).ToList() };
    }
}

public sealed class FacultySmartNotificationService : IFacultySmartNotificationService
{
    private readonly IFacultyDashboardService _dashboard;
    private readonly IWorkspacePreferenceService _preferences;

    public FacultySmartNotificationService(
        IFacultyDashboardService dashboard,
        IWorkspacePreferenceService preferences)
    {
        _dashboard = dashboard;
        _preferences = preferences;
    }

    public async Task<FacultySmartNotificationsDto> GetSmartAsync(CancellationToken cancellationToken = default)
    {
        var today = await _dashboard.GetTodayAsync(null, cancellationToken);
        var prefs = await _preferences.GetAsync(cancellationToken);
        var enabled = prefs.NotificationPreferences;
        bool On(string key) => !enabled.TryGetValue(key, out var v) || v;

        var items = new List<FacultyScheduleNotificationDto>();

        if (On("UpcomingClass") && today.NextClass is not null)
        {
            items.Add(new FacultyScheduleNotificationDto
            {
                NotificationId = $"upcoming-{today.NextClass.TimetableEntryId}",
                Kind = "UpcomingClass",
                Title = "Upcoming class",
                Message = $"{today.NextClass.SubjectName} @ {today.NextClass.RoomName}",
                OccurredUtc = DateTime.UtcNow,
                TimetableId = today.NextClass.TimetableId,
                EntryId = today.NextClass.TimetableEntryId
            });
        }

        if (On("AttendanceReminder") && today.CurrentClass is { AttendanceStatus: not "Completed" })
        {
            items.Add(new FacultyScheduleNotificationDto
            {
                NotificationId = $"attend-{today.CurrentClass.TimetableEntryId}",
                Kind = "AttendanceReminder",
                Title = "Attendance reminder",
                Message = $"Take attendance for {today.CurrentClass.SubjectName}",
                OccurredUtc = DateTime.UtcNow,
                EntryId = today.CurrentClass.TimetableEntryId
            });
        }

        if (On("AiReviewPending"))
        {
            foreach (var r in today.PendingReviews.Take(5))
            {
                items.Add(new FacultyScheduleNotificationDto
                {
                    NotificationId = $"ai-{r.AttendanceSessionId}",
                    Kind = "AiReviewPending",
                    Title = "AI review pending",
                    Message = r.Label,
                    OccurredUtc = r.UpdatedUtc ?? DateTime.UtcNow
                });
            }
        }

        foreach (var n in today.Notifications)
        {
            var key = n.Kind switch
            {
                "RoomChanged" => "RoomChanged",
                "FacultySubstitution" => "FacultySubstitution",
                "Holiday" => "HolidayUpdate",
                "WorkingDayChange" => "WorkingDayChange",
                _ => n.Kind
            };
            if (On(key) || On("RoomChanged") && n.Kind == "Rescheduled")
                items.Add(n);
        }

        return new FacultySmartNotificationsDto { Items = items.Take(40).ToList() };
    }
}
