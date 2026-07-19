using System.Text.Json;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentDeadLetterService : IEnrollmentDeadLetterService
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public EnrollmentDeadLetterService(
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task PersistAsync(EnrollmentDeadLetterRequest request, CancellationToken cancellationToken = default)
    {
        var workItem = request.WorkItem;
        var entry = new EnrollmentDeadLetterEntry
        {
            Id = Guid.NewGuid(),
            ItemId = workItem.ItemId,
            BatchId = workItem.BatchId,
            TenantId = workItem.TenantId,
            StudentId = workItem.StudentId,
            FailureReason = request.FailureReason,
            FailureCode = request.FailureCode,
            ExceptionSummary = request.ExceptionSummary,
            RetryCount = workItem.RetryCount,
            CorrelationId = workItem.CorrelationId,
            CreatedUtc = _clock.GetUtcNow().UtcDateTime,
            RetryHistoryJson = request.RetryHistory == null
                ? null
                : JsonSerializer.Serialize(request.RetryHistory),
        };

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _context.AddAsync(entry);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
