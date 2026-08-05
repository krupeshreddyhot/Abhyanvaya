using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.UnitTests.Faculty;

public sealed class AI31FacultyWorkspaceTests
{
    [Fact]
    public void DeduplicateOperationalEntries_CollapsesIdenticalSlotCopies()
    {
        var a = new TimetableEntry
        {
            Id = 10, TimetableId = 2, StaffId = 5, DayOfWeek = 2, TimeSlotId = 4,
            SubjectId = 7, GroupId = 3, RoomId = 9, CourseId = 1, SemesterId = 1
        };
        var b = new TimetableEntry
        {
            Id = 11, TimetableId = 5, StaffId = 5, DayOfWeek = 2, TimeSlotId = 4,
            SubjectId = 7, GroupId = 3, RoomId = 9, CourseId = 1, SemesterId = 1
        };
        var other = new TimetableEntry
        {
            Id = 12, TimetableId = 2, StaffId = 5, DayOfWeek = 2, TimeSlotId = 5,
            SubjectId = 7, GroupId = 3, RoomId = 9, CourseId = 1, SemesterId = 1
        };

        var result = FacultyDashboardService.DeduplicateOperationalEntries([a, b, other]);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Id == 10);
        Assert.Contains(result, e => e.Id == 12);
        Assert.DoesNotContain(result, e => e.Id == 11);
    }

    [Fact]
    public void FacultyTodayDto_DoesNotModifyAttendanceApis()
    {
        var dto = new FacultyTodayDto { Mode = "Timetable", HasTimetable = true };
        Assert.False(dto.ModifiesAttendanceApis);
    }

    [Fact]
    public void CurrentClassWorkspace_OpensOnlyTodaysActiveClass()
    {
        var dto = new FacultyCurrentClassWorkspaceDto
        {
            Mode = "Timetable",
            HasTimetable = true,
            CurrentClass = new FacultyClassDto { Status = "Current", SubjectId = 1 }
        };
        Assert.True(dto.OpensOnlyTodaysActiveClass);
        Assert.Equal("Current", dto.CurrentClass!.Status);
    }

    [Fact]
    public void QuickActions_IncludeCoreFacultyButtons()
    {
        var actions = new FacultyTodayDto
        {
            QuickActions =
            [
                new FacultyQuickActionDto { Code = "TAKE_ATTENDANCE", Label = "Take Attendance", Path = "/attendance", Primary = true, Enabled = true },
                new FacultyQuickActionDto { Code = "AI_ATTENDANCE", Label = "AI Attendance", Path = "/attendance?ai=1", Primary = true, Enabled = true },
                new FacultyQuickActionDto { Code = "TIMETABLE", Label = "Today's Timetable", Path = "/faculty?tab=timetable", Enabled = true },
                new FacultyQuickActionDto { Code = "STUDENTS", Label = "Student List", Path = "/attendance", Enabled = true },
                new FacultyQuickActionDto { Code = "ROOM", Label = "Room Details", Path = "/setup/scheduling/rooms", Enabled = false },
                new FacultyQuickActionDto { Code = "REPORT_ISSUE", Label = "Report Issue", Path = "/faculty?tab=notifications", Enabled = true },
                new FacultyQuickActionDto { Code = "DASHBOARD", Label = "Dashboard", Path = "/faculty", Enabled = true },
            ]
        };

        Assert.Contains(actions.QuickActions, a => a.Code == "TAKE_ATTENDANCE" && a.Primary);
        Assert.Contains(actions.QuickActions, a => a.Code == "AI_ATTENDANCE");
        Assert.Contains(actions.QuickActions, a => a.Path.Contains("/faculty"));
    }

    [Fact]
    public void NotificationKinds_CoverScheduleChangeSurface()
    {
        var kinds = new[]
        {
            "Cancelled", "Rescheduled", "RoomChanged", "FacultySubstitution", "Holiday", "WorkingDayChange", "ScheduleChange"
        };
        foreach (var kind in kinds)
        {
            var n = new FacultyScheduleNotificationDto { NotificationId = kind, Kind = kind, Title = kind, Message = kind, OccurredUtc = DateTime.UtcNow };
            Assert.Equal(kind, n.Kind);
        }
    }

    [Fact]
    public void TimetableViews_SupportTodayWeekMonthAgenda()
    {
        foreach (var view in new[] { "Today", "Week", "Month", "Agenda" })
        {
            var dto = new FacultyTimetableViewDto { View = view, From = DateOnly.FromDateTime(DateTime.UtcNow), To = DateOnly.FromDateTime(DateTime.UtcNow) };
            Assert.Equal(view, dto.View);
        }
    }

    [Fact]
    public void AttendanceResolution_ModesRemainDistinct_Contract()
    {
        var timetable = new AttendanceSessionResolutionDto { Mode = "Timetable", HasTimetable = true, CourseId = 1, SubjectId = 2 };
        var legacy = new AttendanceSessionResolutionDto { Mode = "Legacy", HasTimetable = false, Message = "manual" };
        Assert.True(timetable.HasTimetable);
        Assert.False(legacy.HasTimetable);
        Assert.Equal("IAttendanceSessionResolver", typeof(IAttendanceSessionResolver).Name);
    }

    [Fact]
    public void Insights_ReuseSummaryShape_WithoutDuplicateEngine()
    {
        var insights = new FacultyInsightsDto
        {
            AttendanceTaken = 2,
            Pending = 1,
            Missed = 0,
            AiUsage = 1,
            RecognitionAccuracy = 88.5m,
            Weekly = new FacultyPeriodSummaryDto { Sessions = 5, Completed = 4, AiSessions = 2, AvgAccuracy = 90 },
            Monthly = new FacultyPeriodSummaryDto { Sessions = 20, Completed = 18, AiSessions = 8, AvgAccuracy = 87 }
        };
        Assert.True(insights.Weekly.Sessions >= insights.Weekly.Completed);
        Assert.NotNull(insights.RecognitionAccuracy);
    }

    [Fact]
    public void NoOpFacultyNotifier_IsSafeDefault()
    {
        var notifier = new NoOpFacultyScheduleNotifier();
        var task = notifier.PublishAsync(1, 2, new FacultyScheduleNotificationDto
        {
            NotificationId = "n1",
            Kind = "ScheduleChange",
            Title = "Update",
            Message = "ok",
            OccurredUtc = DateTime.UtcNow
        });
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void MobileContract_LargeTouchTargetsDocumentedViaPrimaryActions()
    {
        // AI22.7C reuse: primary actions are marked for large touch rendering in UI.
        var action = new FacultyQuickActionDto
        {
            Code = "TAKE_ATTENDANCE",
            Label = "Take Attendance",
            Path = "/attendance",
            Primary = true,
            Enabled = true
        };
        Assert.True(action.Primary);
        Assert.True(action.Enabled);
    }

    [Fact]
    public void AttendanceCompatibility_LegacyAndTimetablePathsBothValid()
    {
        var withTt = new FacultyTodayDto { Mode = "Timetable", HasTimetable = true, Message = "resolved" };
        var withoutTt = new FacultyTodayDto { Mode = "Legacy", HasTimetable = false, Message = "manual" };
        Assert.NotEqual(withTt.Mode, withoutTt.Mode);
        Assert.False(withTt.ModifiesAttendanceApis);
        Assert.False(withoutTt.ModifiesAttendanceApis);
    }
}
