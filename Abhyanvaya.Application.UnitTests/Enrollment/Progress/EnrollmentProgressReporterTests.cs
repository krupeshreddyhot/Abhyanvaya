using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Progress;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Enrollment;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Progress;

public sealed class EnrollmentProgressReporterTests
{
    private readonly Mock<IStudentEnrollmentBatchRepository> _batchRepository = new();
    private readonly Mock<IStudentEnrollmentItemRepository> _itemRepository = new();
    private readonly Mock<IEnrollmentProgressSnapshotRepository> _snapshotRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private EnrollmentProgressReporter CreateReporter() =>
        new(
            _batchRepository.Object,
            _itemRepository.Object,
            _snapshotRepository.Object,
            _unitOfWork.Object,
            TimeProvider.System,
            NullLogger<EnrollmentProgressReporter>.Instance);

    [Fact]
    public async Task TransitionItemAsync_ReturnsNotApplied_WhenFromStatusMismatch()
    {
        var reporter = CreateReporter();
        var item = CreateItem(EnrollmentStatus.Validating);
        var batch = CreateBatch();

        _itemRepository.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _batchRepository.Setup(r => r.GetBatchAsync(batch.Id, batch.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(batch);

        var result = await reporter.TransitionItemAsync(new EnrollmentTransitionRequest
        {
            ItemId = item.Id,
            BatchId = batch.Id,
            TenantId = batch.TenantId,
            FromStatus = EnrollmentStatus.Downloading,
            ToStatus = EnrollmentStatus.Downloaded,
        });

        Assert.False(result.Applied);
        Assert.False(result.ConcurrencyConflict);
    }

    [Fact]
    public async Task MarkItemStartedAsync_TransitionsPendingToDownloading()
    {
        var reporter = CreateReporter();
        var batch = CreateBatch(pending: 1);
        var item = CreateItem(EnrollmentStatus.Pending, batch.Id, batch.TenantId);

        SetupSuccessfulTransition(item, batch);

        var result = await reporter.MarkItemStartedAsync(new EnrollmentProgressOperationRequest
        {
            ItemId = item.Id,
            BatchId = batch.Id,
            TenantId = batch.TenantId,
            ExpectedStatus = EnrollmentStatus.Pending,
        });

        Assert.True(result.Applied);
        Assert.Equal(EnrollmentStatus.Downloading, item.Status);
        Assert.Equal(BatchStatus.Running, batch.Status);
        _batchRepository.Verify(r => r.UpdateBatchAsync(batch, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransitionItemAsync_ReturnsConflict_OnDbUpdateConcurrencyException()
    {
        var reporter = CreateReporter();
        var batch = CreateBatch(pending: 1);
        var item = CreateItem(EnrollmentStatus.Pending, batch.Id, batch.TenantId);

        _itemRepository.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _batchRepository.Setup(r => r.GetBatchAsync(batch.Id, batch.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        var result = await reporter.TransitionItemAsync(new EnrollmentTransitionRequest
        {
            ItemId = item.Id,
            BatchId = batch.Id,
            TenantId = batch.TenantId,
            FromStatus = EnrollmentStatus.Pending,
            ToStatus = EnrollmentStatus.Downloading,
            StampTimestamp = EnrollmentStageTimestamp.DownloadStarted,
        });

        Assert.False(result.Applied);
        Assert.True(result.ConcurrencyConflict);
    }

    [Fact]
    public async Task FinalizeBatchIfCompleteAsync_SetsCompleted_WhenAllItemsTerminalSuccess()
    {
        var reporter = CreateReporter();
        var batch = CreateBatch(completed: 2, total: 2);
        batch.Status = BatchStatus.Running;

        _batchRepository.Setup(r => r.GetBatchAsync(batch.Id, batch.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));

        await reporter.FinalizeBatchIfCompleteAsync(batch.Id, batch.TenantId);

        Assert.Equal(BatchStatus.Completed, batch.Status);
        Assert.NotNull(batch.CompletedUtc);
    }

    private void SetupSuccessfulTransition(StudentEnrollmentItem item, StudentEnrollmentBatch batch)
    {
        _itemRepository.Setup(r => r.GetByIdAsync(item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        _batchRepository.Setup(r => r.GetBatchAsync(batch.Id, batch.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        _unitOfWork
            .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) => await action(ct));
    }

    private static StudentEnrollmentItem CreateItem(
        EnrollmentStatus status,
        Guid? batchId = null,
        int tenantId = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BatchId = batchId ?? Guid.NewGuid(),
            StudentId = 10,
            Status = status,
            SourceUrl = "https://example.com/a.jpg",
            CreatedUtc = DateTime.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

    private static StudentEnrollmentBatch CreateBatch(
        int pending = 0,
        int completed = 0,
        int total = 0)
    {
        var batchId = Guid.NewGuid();
        return new StudentEnrollmentBatch
        {
            Id = batchId,
            TenantId = 1,
            UniversityId = 1,
            CollegeId = 1,
            AcademicYear = 2026,
            PhotoProviderName = "ExamBranch",
            CorrelationId = Guid.NewGuid(),
            TotalStudents = total == 0 ? pending + completed : total,
            PendingCount = pending,
            CompletedCount = completed,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
    }
}
