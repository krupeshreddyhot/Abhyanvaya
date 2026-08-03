using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A;

public sealed class ScheduleVersionServiceTests
{
    [Fact]
    public async Task CreateAsync_AssignsNextVersionNumber()
    {
        var repository = new Mock<IScheduleVersionRepository>();
        ScheduleVersion? created = null;
        repository.Setup(r => r.GetNextVersionNumberAsync(1, 10, null, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        repository.Setup(r => r.AddAsync(It.IsAny<ScheduleVersion>(), It.IsAny<CancellationToken>()))
            .Callback<ScheduleVersion, CancellationToken>((v, _) => created = v)
            .Returns(Task.CompletedTask);
        repository.Setup(r => r.GetByIdAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, int id, CancellationToken _) => created ?? new ScheduleVersion { Id = id, TenantId = 1, AcademicYearId = 10, VersionNumber = 3, VersionName = "V3", Status = ScheduleVersionStatus.Draft });

        var timetableRepository = new Mock<ITimetableRepository>();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.SchedulingAcademicYears).Returns(new[] { new AcademicYear { Id = 10, TenantId = 1, Name = "2026" } }.AsAsyncQueryable());
        context.Setup(c => c.SchedulingAcademicTerms).Returns(Array.Empty<AcademicTerm>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingTimetables).Returns(Array.Empty<Timetable>().AsAsyncQueryable());
        context.Setup(c => c.SchedulingScheduleVersions).Returns(Array.Empty<ScheduleVersion>().AsAsyncQueryable());

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.TenantId).Returns(1);

        var valid = new ValidationResult();
        context.Setup(c => c.SchedulingArchiveReasons).Returns(Array.Empty<ArchiveReasonLookup>().AsAsyncQueryable());
        var service = new ScheduleVersionService(
            repository.Object,
            timetableRepository.Object,
            Mock.Of<IArchiveReasonRepository>(),
            context.Object,
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<IValidator<CreateScheduleVersionRequest>>(v => v.ValidateAsync(It.IsAny<CreateScheduleVersionRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<DuplicateScheduleVersionRequest>>(v => v.ValidateAsync(It.IsAny<DuplicateScheduleVersionRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)));

        await service.CreateAsync(new CreateScheduleVersionRequest { AcademicYearId = 10, VersionName = "V3" });

        Assert.NotNull(created);
        Assert.Equal(3, created!.VersionNumber);
        Assert.Equal(ScheduleVersionStatus.Draft, created.Status);
    }
}
