using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2;

public sealed class TimetableStatusTransitionTests
{
    private readonly Mock<ITimetableRepository> _repository = new();
    private readonly Mock<ISubjectAllocationRepository> _allocationRepository = new();
    private readonly Mock<ITimeSlotRepository> _timeSlotRepository = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public TimetableStatusTransitionTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(1);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _context.Setup(c => c.SchedulingAcademicYears).Returns(Array.Empty<Abhyanvaya.Domain.Entities.Scheduling.AcademicYear>().AsAsyncQueryable());
        _context.Setup(c => c.Departments).Returns(Array.Empty<Abhyanvaya.Domain.Entities.Department>().AsAsyncQueryable());
        _context.Setup(c => c.SchedulingTimeSlotSets).Returns(Array.Empty<Abhyanvaya.Domain.Entities.Scheduling.TimeSlotSet>().AsAsyncQueryable());
    }

    [Fact]
    public async Task Lock_FromDraft_Succeeds()
    {
        var timetable = new Timetable { Id = 1, Status = TimetableStatus.Draft, Name = "T", AcademicYearId = 1 };
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(timetable);
        _repository.Setup(r => r.CountEntriesAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var service = CreateService();
        var result = await service.LockAsync(1);

        Assert.Equal(TimetableStatus.Locked, result.Status);
        Assert.Equal(TimetableStatus.Locked, timetable.Status);
    }

    [Fact]
    public async Task Lock_WhenNotDraft_ThrowsDomainException()
    {
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Timetable { Id = 1, Status = TimetableStatus.Locked, Name = "T" });

        var service = CreateService();
        await Assert.ThrowsAsync<DomainException>(() => service.LockAsync(1));
    }

    [Fact]
    public async Task Unlock_FromLocked_Succeeds()
    {
        var timetable = new Timetable { Id = 1, Status = TimetableStatus.Locked, Name = "T", AcademicYearId = 1 };
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(timetable);
        _repository.Setup(r => r.CountEntriesAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var service = CreateService();
        var result = await service.UnlockAsync(1);

        Assert.Equal(TimetableStatus.Draft, result.Status);
        Assert.Equal(TimetableStatus.Draft, timetable.Status);
    }

    [Fact]
    public async Task Unlock_WhenNotLocked_ThrowsDomainException()
    {
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Timetable { Id = 1, Status = TimetableStatus.Draft, Name = "T" });

        var service = CreateService();
        await Assert.ThrowsAsync<DomainException>(() => service.UnlockAsync(1));
    }

    [Theory]
    [InlineData(TimetableStatus.Locked)]
    public void EnsureDraft_WhenNonDraftStatus_Throws(TimetableStatus status)
    {
        var timetable = new Timetable { Status = status };
        var ex = Assert.Throws<DomainException>(() => TimetableService.EnsureDraft(timetable));
        Assert.Contains("Draft", ex.Message);
    }

    [Theory]
    [InlineData(TimetableStatus.Published)]
    [InlineData(TimetableStatus.Archived)]
    public void EnsureDraft_WhenReadOnlyStatus_Throws(TimetableStatus status)
    {
        var timetable = new Timetable { Status = status };
        var ex = Assert.Throws<DomainException>(() => TimetableService.EnsureDraft(timetable));
        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private TimetableService CreateService()
    {
        var valid = new ValidationResult();
        return new TimetableService(
            _repository.Object,
            _allocationRepository.Object,
            _timeSlotRepository.Object,
            _context.Object,
            _unitOfWork.Object,
            _currentUser.Object,
            Mock.Of<IValidator<CreateTimetableRequest>>(v => v.ValidateAsync(It.IsAny<CreateTimetableRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<UpdateTimetableRequest>>(v => v.ValidateAsync(It.IsAny<UpdateTimetableRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<CreateTimetableEntryRequest>>(v => v.ValidateAsync(It.IsAny<CreateTimetableEntryRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<UpdateTimetableEntryRequest>>(v => v.ValidateAsync(It.IsAny<UpdateTimetableEntryRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<BulkPasteEntriesRequest>>(v => v.ValidateAsync(It.IsAny<BulkPasteEntriesRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<MoveTimetableEntryRequest>>(v => v.ValidateAsync(It.IsAny<MoveTimetableEntryRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<CopyTimetableEntryRequest>>(v => v.ValidateAsync(It.IsAny<CopyTimetableEntryRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)));
    }
}
