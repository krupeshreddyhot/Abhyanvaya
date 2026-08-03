namespace Abhyanvaya.Application.DTOs.Faculty;

public sealed class FacultyTodayDto
{
    public DateOnly Date { get; init; }
    public int? StaffId { get; init; }
    public string Mode { get; init; } = "Legacy";
    public bool HasTimetable { get; init; }
    public string Message { get; init; } = "";
    public FacultyClassDto? CurrentClass { get; init; }
    public FacultyClassDto? NextClass { get; init; }
    public IReadOnlyList<FacultyClassDto> TodaysSchedule { get; init; } = [];
    public FacultyAttendanceSummaryDto AttendanceSummary { get; init; } = new();
    public FacultyAiAttendanceSummaryDto AiAttendanceSummary { get; init; } = new();
    public IReadOnlyList<FacultyPendingReviewDto> PendingReviews { get; init; } = [];
    public IReadOnlyList<FacultyScheduleNotificationDto> Notifications { get; init; } = [];
    public IReadOnlyList<FacultyQuickActionDto> QuickActions { get; init; } = [];
    public DateTime GeneratedUtc { get; init; }
    public bool ModifiesAttendanceApis => false;
}

public sealed class FacultyClassDto
{
    public int? TimetableEntryId { get; init; }
    public int? TimetableId { get; init; }
    public string Status { get; init; } = "Upcoming"; // Current | Upcoming | Completed
    public byte DayOfWeek { get; init; }
    public int? TimeSlotId { get; init; }
    public int? PeriodNumber { get; init; }
    public TimeSpan? StartTime { get; init; }
    public TimeSpan? EndTime { get; init; }
    public int? MinutesRemaining { get; init; }
    public int CourseId { get; init; }
    public int GroupId { get; init; }
    public int SemesterId { get; init; }
    public int SubjectId { get; init; }
    public string? SubjectName { get; init; }
    public int? RoomId { get; init; }
    public string? RoomName { get; init; }
    public string? BuildingName { get; init; }
    public string? FloorName { get; init; }
    public int? StudentCount { get; init; }
    public string AttendanceStatus { get; init; } = "NotStarted"; // NotStarted | InProgress | Completed
    public string? AiCaptureStatus { get; init; }
    public Guid? AttendanceSessionId { get; init; }
}

public sealed class FacultyAttendanceSummaryDto
{
    public int ClassesToday { get; init; }
    public int AttendanceTaken { get; init; }
    public int Pending { get; init; }
    public int Missed { get; init; }
    public int PresentMarks { get; init; }
    public int AbsentMarks { get; init; }
}

public sealed class FacultyAiAttendanceSummaryDto
{
    public int SessionsToday { get; init; }
    public int PendingReviews { get; init; }
    public decimal? AverageRecognitionAccuracy { get; init; }
    public int AiUsageCount { get; init; }
}

public sealed class FacultyPendingReviewDto
{
    public Guid AttendanceSessionId { get; init; }
    public string Label { get; init; } = "";
    public int PendingCount { get; init; }
    public DateTime? UpdatedUtc { get; init; }
    public string ReviewPath { get; init; } = "";
}

public sealed class FacultyScheduleNotificationDto
{
    public string NotificationId { get; init; } = "";
    public string Kind { get; init; } = "ScheduleChange"; // Cancelled | Rescheduled | RoomChanged | FacultySubstitution | Holiday | WorkingDayChange | ScheduleChange
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime OccurredUtc { get; init; }
    public int? TimetableId { get; init; }
    public int? EntryId { get; init; }
}

public sealed class FacultyQuickActionDto
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Primary { get; init; }
    public bool Enabled { get; init; } = true;
    public string? Hint { get; init; }
}

public sealed class FacultyTimetableViewDto
{
    public string View { get; init; } = "Today"; // Today | Week | Month | Agenda
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public IReadOnlyList<FacultyClassDto> Classes { get; init; } = [];
}

public sealed class FacultyInsightsDto
{
    public int AttendanceTaken { get; init; }
    public int Pending { get; init; }
    public int Missed { get; init; }
    public double? AverageCompletionMinutes { get; init; }
    public int AiUsage { get; init; }
    public decimal? RecognitionAccuracy { get; init; }
    public FacultyPeriodSummaryDto Weekly { get; init; } = new();
    public FacultyPeriodSummaryDto Monthly { get; init; } = new();
}

public sealed class FacultyPeriodSummaryDto
{
    public int Sessions { get; init; }
    public int Completed { get; init; }
    public int AiSessions { get; init; }
    public decimal? AvgAccuracy { get; init; }
}

public sealed class FacultyCurrentClassWorkspaceDto
{
    public FacultyClassDto? CurrentClass { get; init; }
    public string Mode { get; init; } = "Legacy";
    public bool HasTimetable { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<FacultyQuickActionDto> QuickActions { get; init; } = [];
    public bool OpensOnlyTodaysActiveClass => true;
}
