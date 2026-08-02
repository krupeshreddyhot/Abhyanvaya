using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2B;

public sealed class AttendanceSessionResolverTests
{
    [Fact]
    public async Task Resolve_WhenNoStaff_ReturnsLegacyMode()
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.SchedulingAcademicYears).Returns(Array.Empty<AcademicYear>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimetables).Returns(Array.Empty<Timetable>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimetableEntries).Returns(Array.Empty<TimetableEntry>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimeSlots).Returns(Array.Empty<TimeSlot>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingRooms).Returns(Array.Empty<Room>().AsAsyncQueryable());
        context.Setup(c => c.Subjects).Returns(Array.Empty<Domain.Entities.Subject>().AsAsyncQueryable());
        context.Setup(c => c.TenantSubjects).Returns(Array.Empty<Domain.Entities.TenantSubject>().AsAsyncQueryable());

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.TenantId).Returns(1);
        user.SetupGet(u => u.StaffId).Returns(0);

        var resolver = new AttendanceSessionResolver(context.Object, user.Object);
        var result = await resolver.ResolveAsync(null, new DateOnly(2026, 8, 2));

        Assert.Equal("Legacy", result.Mode);
        Assert.False(result.HasTimetable);
        Assert.Contains("Course", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_WhenPublishedTimetableExists_ReturnsTimetableMode()
    {
        var year = new AcademicYear { Id = 1, TenantId = 1, IsCurrent = true, Name = "Y", Code = "Y" };
        var timetable = new Timetable
        {
            Id = 10,
            TenantId = 1,
            AcademicYearId = 1,
            Status = TimetableStatus.Published,
            Name = "T",
            Code = "T"
        };
        var slot = new TimeSlot
        {
            Id = 5,
            TenantId = 1,
            Name = "P1",
            PeriodNumber = 2,
            SlotKind = SlotKind.Period,
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.FromHours(23)
        };
        var entry = new TimetableEntry
        {
            Id = 100,
            TenantId = 1,
            TimetableId = 10,
            StaffId = 42,
            DayOfWeek = (byte)DateTime.UtcNow.DayOfWeek,
            TimeSlotId = 5,
            CourseId = 3,
            GroupId = 4,
            SemesterId = 5,
            SubjectId = 6,
            RoomId = 7,
            DepartmentId = 1,
            SubjectAllocationId = 1
        };

        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.SchedulingAcademicYears).Returns(new[] { year }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimetables).Returns(new[] { timetable }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimetableEntries).Returns(new[] { entry }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimeSlots).Returns(new[] { slot }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingRooms).Returns(new[] { new Room { Id = 7, Name = "Lab-1", Code = "L1", FloorId = 1 } }.AsAsyncQueryable());
        context.Setup(c => c.Subjects).Returns(new[] { new Domain.Entities.Subject { Id = 6, TenantSubjectId = 60 } }.AsAsyncQueryable());
        context.Setup(c => c.TenantSubjects).Returns(new[] { new Domain.Entities.TenantSubject { Id = 60, Name = "Physics" } }.AsAsyncQueryable());

        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.TenantId).Returns(1);
        user.SetupGet(u => u.StaffId).Returns(42);

        var resolver = new AttendanceSessionResolver(context.Object, user.Object);
        var result = await resolver.ResolveAsync(42, DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal("Timetable", result.Mode);
        Assert.True(result.HasTimetable);
        Assert.Equal(3, result.CourseId);
        Assert.Equal(4, result.GroupId);
        Assert.Equal(5, result.SemesterId);
        Assert.Equal(6, result.SubjectId);
        Assert.Equal(2, result.PeriodNumber);
    }
}
