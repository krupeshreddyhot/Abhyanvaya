using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A;

public sealed class TimetableChangeHistoryTests
{
    [Fact]
    public async Task RecordAsync_PersistsHistoryEntry()
    {
        TimetableChangeHistory? captured = null;
        var repository = new Mock<ITimetableChangeHistoryRepository>();
        repository.Setup(r => r.AddAsync(It.IsAny<TimetableChangeHistory>(), It.IsAny<CancellationToken>()))
            .Callback<TimetableChangeHistory, CancellationToken>((e, _) => captured = e)
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.TenantId).Returns(1);
        currentUser.Setup(x => x.UserId).Returns(42);

        var service = new TimetableChangeHistoryService(
            repository.Object,
            unitOfWork.Object,
            currentUser.Object,
            new NoOpFacultyScheduleNotifier());
        await service.RecordAsync(5, TimetableChangeOperation.Lock, null, new { Status = TimetableStatus.Draft }, new { Status = TimetableStatus.Locked }, "test");

        Assert.NotNull(captured);
        Assert.Equal(5, captured!.TimetableId);
        Assert.Equal(TimetableChangeOperation.Lock, captured.Operation);
        Assert.Equal(42, captured.UserId);
        repository.Verify(r => r.AddAsync(It.IsAny<TimetableChangeHistory>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
