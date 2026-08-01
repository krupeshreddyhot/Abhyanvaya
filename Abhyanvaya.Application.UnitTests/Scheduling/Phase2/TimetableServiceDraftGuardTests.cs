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

public sealed class TimetableServiceDraftGuardTests
{
    private readonly Mock<ITimetableRepository> _repository = new();
    private readonly Mock<ISubjectAllocationRepository> _allocationRepository = new();
    private readonly Mock<ITimeSlotRepository> _timeSlotRepository = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IValidator<CreateTimetableRequest>> _createTimetableValidator = new();
    private readonly Mock<IValidator<UpdateTimetableRequest>> _updateTimetableValidator = new();
    private readonly Mock<IValidator<CreateTimetableEntryRequest>> _createEntryValidator = new();
    private readonly Mock<IValidator<UpdateTimetableEntryRequest>> _updateEntryValidator = new();
    private readonly Mock<IValidator<BulkPasteEntriesRequest>> _bulkValidator = new();
    private readonly Mock<IValidator<MoveTimetableEntryRequest>> _moveValidator = new();
    private readonly Mock<IValidator<CopyTimetableEntryRequest>> _copyValidator = new();

    public TimetableServiceDraftGuardTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(1);
        SetupValidValidators();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task UpdateTimetable_WhenLocked_ThrowsDomainException()
    {
        _repository.Setup(r => r.GetByIdAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Timetable { Id = 10, Status = TimetableStatus.Locked, Name = "T" });

        var service = CreateService();
        var act = () => service.UpdateTimetableAsync(new UpdateTimetableRequest
        {
            Id = 10,
            Name = "Updated",
            AcademicYearId = 1
        });

        var ex = await Assert.ThrowsAsync<DomainException>(act);
        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public async Task CreateEntry_WhenLocked_ThrowsDomainException()
    {
        _repository.Setup(r => r.GetByIdAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Timetable { Id = 10, Status = TimetableStatus.Locked, Name = "T" });

        var service = CreateService();
        var act = () => service.CreateEntryAsync(10, new CreateTimetableEntryRequest
        {
            DayOfWeek = 1,
            TimeSlotId = 5,
            SubjectAllocationId = 3,
            RoomId = 7
        });

        var ex = await Assert.ThrowsAsync<DomainException>(act);
        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public async Task DeleteEntry_WhenLocked_ThrowsDomainException()
    {
        _repository.Setup(r => r.GetEntryByIdAsync(1, 99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableEntry { Id = 99, TimetableId = 10 });
        _repository.Setup(r => r.GetByIdAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Timetable { Id = 10, Status = TimetableStatus.Locked, Name = "T" });

        var service = CreateService();
        var act = () => service.DeleteEntryAsync(99);
        await Assert.ThrowsAsync<DomainException>(act);
    }

    private TimetableService CreateService() => new(
        _repository.Object,
        _allocationRepository.Object,
        _timeSlotRepository.Object,
        _context.Object,
        _unitOfWork.Object,
        _currentUser.Object,
        _createTimetableValidator.Object,
        _updateTimetableValidator.Object,
        _createEntryValidator.Object,
        _updateEntryValidator.Object,
        _bulkValidator.Object,
        _moveValidator.Object,
        _copyValidator.Object);

    private void SetupValidValidators()
    {
        var valid = new ValidationResult();
        _createTimetableValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateTimetableRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        _updateTimetableValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateTimetableRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        _createEntryValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateTimetableEntryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        _updateEntryValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateTimetableEntryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        _bulkValidator.Setup(v => v.ValidateAsync(It.IsAny<BulkPasteEntriesRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        _moveValidator.Setup(v => v.ValidateAsync(It.IsAny<MoveTimetableEntryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
        _copyValidator.Setup(v => v.ValidateAsync(It.IsAny<CopyTimetableEntryRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(valid);
    }
}
