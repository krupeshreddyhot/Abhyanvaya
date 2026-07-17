using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Background;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Abhyanvaya.Application.Enrollment.Pipeline;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.Enrollment.Background;

public sealed class EnrollmentWorkScheduler : IEnrollmentWorkScheduler
{
    private readonly IEnrollmentWorkRepository _workRepository;
    private readonly IEnrollmentSchedulingPolicy _schedulingPolicy;
    private readonly IEnrollmentRetryPolicy _retryPolicy;
    private readonly IEnrollmentDeadLetterService _deadLetterService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly EnrollmentRecoveryOptions _recoveryOptions;
    private readonly TimeProvider _clock;
    private readonly ILogger<EnrollmentWorkScheduler> _logger;

    public EnrollmentWorkScheduler(
        IEnrollmentWorkRepository workRepository,
        IEnrollmentSchedulingPolicy schedulingPolicy,
        IEnrollmentRetryPolicy retryPolicy,
        IEnrollmentDeadLetterService deadLetterService,
        IUnitOfWork unitOfWork,
        IDistributedLockProvider lockProvider,
        IOptions<EnrollmentRecoveryOptions> recoveryOptions,
        TimeProvider clock,
        ILogger<EnrollmentWorkScheduler> logger)
    {
        _workRepository = workRepository;
        _schedulingPolicy = schedulingPolicy;
        _retryPolicy = retryPolicy;
        _deadLetterService = deadLetterService;
        _unitOfWork = unitOfWork;
        _lockProvider = lockProvider;
        _recoveryOptions = recoveryOptions.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EnrollmentWorkItem?> GetNextWorkAsync(CancellationToken cancellationToken = default)
    {
        await using var claimLock = await _lockProvider.TryAcquireLockAsync(
            "enrollment-work-claim",
            TimeSpan.FromSeconds(5),
            cancellationToken);

        if (claimLock == null)
        {
            return null;
        }

        var work = await _workRepository.ClaimNextAsync(cancellationToken);
        if (work == null)
        {
            return null;
        }

        if (!_schedulingPolicy.IsEligible(work, _clock.GetUtcNow().UtcDateTime))
        {
            return null;
        }

        return work;
    }

    public async Task<EnrollmentRetryScheduleResult> ScheduleRetryAsync(
        EnrollmentRetryScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var fakeStage = new RetryStageAdapter(request.StageName);
        var fakeResult = EnrollmentPipelineStageExecutionResult.Failed(
            EnrollmentPipelineContext.Create(new EnrollmentPipelineRequest
            {
                Context = new EnrollmentItemContext
                {
                    BatchId = request.WorkItem.BatchId,
                    ItemId = request.WorkItem.ItemId,
                    TenantId = request.WorkItem.TenantId,
                    StudentId = request.WorkItem.StudentId,
                    StudentNumber = request.WorkItem.StudentNumber,
                    CollegeCode = request.WorkItem.CollegeCode,
                    CollegeId = request.WorkItem.CollegeId,
                    AcademicYear = request.WorkItem.AcademicYear,
                    PhotoProviderName = request.WorkItem.PhotoProviderName,
                    ExecutionTraceId = Guid.NewGuid(),
                    CorrelationId = request.WorkItem.CorrelationId,
                    PipelineVersion = request.WorkItem.PipelineVersion,
                },
                ItemStatus = request.WorkItem.Status,
            }),
            TimeSpan.Zero,
            request.FailureCode ?? EnrollmentPipelineFailureCodes.StageFailed,
            request.FailureReason ?? "Retry scheduled.",
            request.FailureCategory,
            isRetryable: true,
            retryAttempts: request.AttemptCount);

        var decision = _retryPolicy.Evaluate(fakeStage, fakeResult, request.AttemptCount);
        if (!decision.ShouldRetry)
        {
            if (request.WorkItem.RetryCount >= _recoveryOptions.MaxRetryCount)
            {
                await _deadLetterService.PersistAsync(new EnrollmentDeadLetterRequest
                {
                    WorkItem = request.WorkItem,
                    FailureReason = request.FailureReason ?? "Maximum retry attempts exceeded.",
                    FailureCode = request.FailureCode,
                }, cancellationToken);

                return new EnrollmentRetryScheduleResult
                {
                    Scheduled = false,
                    MovedToDeadLetter = true,
                    Reason = decision.Reason,
                };
            }

            return new EnrollmentRetryScheduleResult
            {
                Scheduled = false,
                MovedToDeadLetter = false,
                Reason = decision.Reason,
            };
        }

        var nextAttemptUtc = _clock.GetUtcNow().UtcDateTime.Add(decision.Delay);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _workRepository.ScheduleRetryAsync(
                request.WorkItem.ItemId,
                nextAttemptUtc,
                request.FailureReason,
                request.FailureCategory,
                ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Retry scheduled. ItemId={ItemId} NextAttemptUtc={NextAttemptUtc} AttemptCount={AttemptCount}",
            request.WorkItem.ItemId,
            nextAttemptUtc,
            request.AttemptCount);

        return new EnrollmentRetryScheduleResult
        {
            Scheduled = true,
            NextAttemptUtc = nextAttemptUtc,
        };
    }

    private sealed class RetryStageAdapter : IEnrollmentPipelineStage
    {
        public RetryStageAdapter(string name) => Name = name;

        public EnrollmentPipelineStage? ManifestStage => null;
        public string Name { get; }
        public int Order => 0;
        public string Description => "Retry adapter";
        public string Version => "1.0";
        public bool SupportsRetry => true;
        public bool SupportsResume => false;

        public Task<EnrollmentPipelineStageExecutionResult> ExecuteAsync(
            EnrollmentPipelineContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
