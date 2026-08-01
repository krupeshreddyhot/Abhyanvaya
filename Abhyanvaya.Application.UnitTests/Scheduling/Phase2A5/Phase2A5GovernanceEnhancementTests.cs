using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A5;

public sealed class Phase2A5GovernanceEnhancementTests
{
    [Theory]
    [InlineData(PermissionKeys.SchedulingVersionCompareView)]
    [InlineData(PermissionKeys.SchedulingVersionCompareExport)]
    [InlineData(PermissionKeys.SchedulingApprovalCommentsView)]
    [InlineData(PermissionKeys.SchedulingApprovalCommentsManage)]
    [InlineData(PermissionKeys.SchedulingFreeze)]
    [InlineData(PermissionKeys.SchedulingUnlock)]
    [InlineData(PermissionKeys.SchedulingArchiveView)]
    [InlineData(PermissionKeys.SchedulingArchiveManage)]
    public void PermissionKeys_All_ContainsPhase2A5Keys(string key) =>
        Assert.Contains(key, PermissionKeys.All);

    [Fact]
    public async Task Freeze_PublishedTimetable_SetsIsFrozen()
    {
        var entity = new Timetable { Id = 1, TenantId = 1, Status = TimetableStatus.Published, Name = "T", IsFrozen = false };
        var repository = new Mock<ITimetableRepository>();
        repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        var history = new Mock<ITimetableChangeHistoryService>();
        var timetableService = new Mock<ITimetableService>();
        timetableService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableDto { Id = 1, Name = "T", Status = TimetableStatus.Published, IsFrozen = true, AcademicYearId = 1 });

        var valid = new ValidationResult();
        var service = new TimetableLifecycleService(
            repository.Object,
            Mock.Of<IScheduleVersionRepository>(),
            Mock.Of<IArchiveReasonRepository>(),
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<IUnitOfWork>(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()) == Task.FromResult(1)),
            Mock.Of<ICurrentUserService>(c => c.TenantId == 1 && c.UserId == 9),
            history.Object,
            timetableService.Object,
            Mock.Of<IValidator<FreezeTimetableRequest>>(v => v.ValidateAsync(It.IsAny<FreezeTimetableRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<UnlockFrozenTimetableRequest>>(v => v.ValidateAsync(It.IsAny<UnlockFrozenTimetableRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)));

        var result = await service.FreezeAsync(1, new FreezeTimetableRequest { Reason = "Exam period" });
        Assert.True(entity.IsFrozen);
        Assert.True(result.IsFrozen);
        Assert.Equal("Exam period", entity.FreezeReason);
    }

    [Fact]
    public void EnsureDraft_WhenFrozen_Throws()
    {
        var timetable = new Timetable { Status = TimetableStatus.Draft, IsFrozen = true };
        Assert.Throws<DomainException>(() => TimetableService.EnsureDraft(timetable));
    }

    [Fact]
    public async Task Compare_DifferentVersions_ReturnsSummary()
    {
        var leftVersion = new ScheduleVersion { Id = 1, TenantId = 1, VersionName = "Draft", Status = ScheduleVersionStatus.Draft, VersionNumber = 1, AcademicYearId = 1 };
        var rightVersion = new ScheduleVersion { Id = 2, TenantId = 1, VersionName = "Published", Status = ScheduleVersionStatus.Published, VersionNumber = 2, AcademicYearId = 1 };
        var versionRepo = new Mock<IScheduleVersionRepository>();
        versionRepo.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(leftVersion);
        versionRepo.Setup(r => r.GetByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(rightVersion);

        var leftEntry = new TimetableEntry { Id = 10, TenantId = 1, TimetableId = 100, DayOfWeek = 1, TimeSlotId = 1, SubjectAllocationId = 5, StaffId = 1, RoomId = 1, SubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 };
        var rightEntry = new TimetableEntry { Id = 11, TenantId = 1, TimetableId = 101, DayOfWeek = 1, TimeSlotId = 1, SubjectAllocationId = 5, StaffId = 2, RoomId = 1, SubjectId = 1, CourseId = 1, GroupId = 1, SemesterId = 1 };
        var compareRepo = new Mock<IVersionComparisonRepository>();
        compareRepo.Setup(r => r.ListEntriesForVersionAsync(1, 1, null, It.IsAny<CancellationToken>())).ReturnsAsync([leftEntry]);
        compareRepo.Setup(r => r.ListEntriesForVersionAsync(1, 2, null, It.IsAny<CancellationToken>())).ReturnsAsync([rightEntry]);

        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.Subjects).Returns(Array.Empty<Domain.Entities.Subject>().AsAsyncQueryable());
        context.Setup(c => c.TenantSubjects).Returns(Array.Empty<Domain.Entities.TenantSubject>().AsAsyncQueryable());
        context.Setup(c => c.StaffMembers).Returns(Array.Empty<Domain.Entities.Staff>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingRooms).Returns(Array.Empty<Room>().AsAsyncQueryable());

        var valid = new ValidationResult();
        var service = new VersionComparisonService(
            versionRepo.Object,
            compareRepo.Object,
            context.Object,
            Mock.Of<ICurrentUserService>(c => c.TenantId == 1),
            Mock.Of<IValidator<CompareScheduleVersionsRequest>>(v => v.ValidateAsync(It.IsAny<CompareScheduleVersionsRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)));

        var result = await service.CompareAsync(new CompareScheduleVersionsRequest { LeftVersionId = 1, RightVersionId = 2 });
        Assert.Equal(1, result.Summary.Modified);
        Assert.Contains(result.Differences, d => d.Category == VersionDifferenceCategory.FacultyAssignment);
    }
}
