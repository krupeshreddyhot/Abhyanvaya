using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A;

public sealed class TimetablePublishingUniquenessTests
{
    private readonly Mock<ITimetableRepository> _repository = new();
    private readonly Mock<IScheduleVersionRepository> _versionRepository = new();
    private readonly Mock<IArchiveReasonRepository> _archiveReasonRepository = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ITimetableChangeHistoryService> _historyService = new();
    private readonly Mock<ITimetableService> _timetableService = new();

    public TimetablePublishingUniquenessTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(1);
        _currentUser.Setup(x => x.UserId).Returns(10);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Publish_WhenAnotherPublishedExists_ThrowsDomainException()
    {
        var entity = new Timetable { Id = 1, TenantId = 1, AcademicYearId = 10, DepartmentId = 3, Status = TimetableStatus.Locked, Name = "T" };
        var existing = new Timetable { Id = 2, TenantId = 1, AcademicYearId = 10, DepartmentId = 3, Status = TimetableStatus.Published, Name = "Existing" };
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _context.Setup(c => c.SchedulingTimetables).Returns(new[] { entity, existing }.AsAsyncQueryable());

        var service = CreateService();
        await Assert.ThrowsAsync<DomainException>(() => service.PublishAsync(1, null));
    }

    [Fact]
    public async Task Publish_FromLocked_Succeeds()
    {
        var entity = new Timetable { Id = 1, TenantId = 1, AcademicYearId = 10, DepartmentId = 3, Status = TimetableStatus.Locked, Name = "T" };
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _context.Setup(c => c.SchedulingTimetables).Returns(new[] { entity }.AsAsyncQueryable());
        _timetableService.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TimetableDto { Id = 1, Name = "T", Status = TimetableStatus.Published, AcademicYearId = 10 });

        var service = CreateService();
        var result = await service.PublishAsync(1, null);

        Assert.Equal(TimetableStatus.Published, result.Status);
        Assert.Equal(TimetableStatus.Published, entity.Status);
    }

    private TimetableLifecycleService CreateService() => new(
        _repository.Object,
        _versionRepository.Object,
        _archiveReasonRepository.Object,
        _context.Object,
        _unitOfWork.Object,
        _currentUser.Object,
        _historyService.Object,
        _timetableService.Object,
        Mock.Of<FluentValidation.IValidator<FreezeTimetableRequest>>(),
        Mock.Of<FluentValidation.IValidator<UnlockFrozenTimetableRequest>>());
}
