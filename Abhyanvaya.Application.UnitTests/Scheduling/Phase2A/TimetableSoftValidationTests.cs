using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.Scheduling.Capacity;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A;

public sealed class TimetableSoftValidationTests
{
    [Fact]
    public async Task ValidateAsync_NeverThrows()
    {
        var timetable = new Timetable { Id = 1, TenantId = 1, AcademicYearId = 10, Name = "T", Status = TimetableStatus.Draft };
        var entry = new TimetableEntry
        {
            Id = 100,
            TenantId = 1,
            TimetableId = 1,
            DayOfWeek = 1,
            TimeSlotId = 5,
            StaffId = 7,
            RoomId = 8,
            SubjectAllocationId = 9,
            DepartmentId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            SubjectId = 1
        };

        var timetableRepository = new Mock<ITimetableRepository>();
        timetableRepository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(timetable);
        timetableRepository.Setup(r => r.ListEntriesAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TimetableEntry> { entry, entry });

        var dismissalRepository = new Mock<ITimetableWarningDismissalRepository>();
        dismissalRepository.Setup(r => r.ListForTimetableAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<TimetableWarningDismissal>());

        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.SchedulingFacultyAvailabilities).Returns(new List<FacultyAvailability>
        {
            new() { TenantId = 1, AcademicYearId = 10, StaffId = 7, AvailabilityType = FacultyAvailabilityType.Unavailable, StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) }
        }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingRoomAvailabilities).Returns(Array.Empty<RoomAvailability>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingRooms).Returns(new List<Room> { new() { Id = 8, Capacity = 20, RoomType = RoomType.Classroom } }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingSubjectAllocations).Returns(new List<SubjectAllocation> { new() { Id = 9, PreferredRoomId = 99, LabRequired = true } }.AsAsyncQueryable());
        context.Setup(c => c.Subjects).Returns(new List<Subject> { new() { Id = 1, ExpectedCapacity = 50 } }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingWorkingDays).Returns(new List<WorkingDay> { new() { DayOfWeek = 1, IsWorking = false, AcademicYearId = 10, TenantId = 1 } }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingHolidays).Returns(Array.Empty<Holiday>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingTeachingGroups).Returns(Array.Empty<TeachingGroup>().AsAsyncQueryable());

        var unitOfWork = new Mock<IUnitOfWork>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.TenantId).Returns(1);
        currentUser.Setup(x => x.UserId).Returns(10);

        var service = new TimetableSoftValidationService(
            timetableRepository.Object,
            dismissalRepository.Object,
            context.Object,
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<IValidator<Abhyanvaya.Application.DTOs.Scheduling.DismissSoftWarningRequest>>(v =>
                v.ValidateAsync(It.IsAny<Abhyanvaya.Application.DTOs.Scheduling.DismissSoftWarningRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(new ValidationResult())),
            Mock.Of<ITeachingGroupMembershipResolver>(),
            PlacementSizeResolver.Instance,
            RoomCapacityEvaluator.Instance,
            Mock.Of<Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.IConflictRuleConfigurationService>(c =>
                c.GetThresholdsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())
                == Task.FromResult(Abhyanvaya.Application.Scheduling.Conflicts.Intelligence.ConflictRuleThresholds.Defaults)),
            SchedulingConflictPresentationComposer.Instance);

        var warnings = await Record.ExceptionAsync(() => service.ValidateAsync(1));
        Assert.Null(warnings);
        var result = await service.ValidateAsync(1);
        Assert.NotEmpty(result);
        Assert.All(result, w => Assert.Contains(w.Severity, new[] { "Warning", "Error", "Information", "Critical" }));
    }
}
