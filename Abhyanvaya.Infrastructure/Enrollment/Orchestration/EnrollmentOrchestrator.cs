using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Orchestration;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Orchestration;

public sealed class EnrollmentOrchestrator : IEnrollmentOrchestrator
{
    private readonly IEnrollmentPipelineExecutor _executor;
    private readonly ILogger<EnrollmentOrchestrator> _logger;

    public EnrollmentOrchestrator(
        IEnrollmentPipelineExecutor executor,
        ILogger<EnrollmentOrchestrator> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public Task<EnrollmentPipelineResult> ProcessItemAsync(
        EnrollmentPipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);

        _logger.LogInformation(
            "Enrollment orchestrator processing item. ItemId={ItemId} BatchId={BatchId} StudentId={StudentId} CorrelationId={CorrelationId}",
            request.Context.ItemId,
            request.Context.BatchId,
            request.Context.StudentId,
            request.Context.CorrelationId);

        var context = EnrollmentPipelineContext.Create(request);
        return _executor.ExecuteAsync(context, cancellationToken);
    }
}
