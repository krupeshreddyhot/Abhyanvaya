using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Abhyanvaya.Application.Recognition.Orchestration;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Recognition.Orchestration;

public sealed class RecognitionOrchestrator : IRecognitionOrchestrator
{
    private readonly IRecognitionPipelineExecutor _executor;
    private readonly ILogger<RecognitionOrchestrator> _logger;

    public RecognitionOrchestrator(
        IRecognitionPipelineExecutor executor,
        ILogger<RecognitionOrchestrator> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public Task<RecognitionResult> RecognizeAsync(
        RecognitionPipelineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Context);

        _logger.LogInformation(
            "Recognition orchestrator processing request. RequestId={RequestId} SessionId={SessionId} FaceIndex={FaceIndex} CorrelationId={CorrelationId}",
            request.Context.RecognitionRequestId,
            request.Context.AttendanceSessionId,
            request.Context.FaceIndex,
            request.Context.CorrelationId);

        var context = RecognitionPipelineContext.Create(request);
        return _executor.ExecuteAsync(context, cancellationToken);
    }
}
