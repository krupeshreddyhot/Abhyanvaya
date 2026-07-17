using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Recognition;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Recognition.Engine;

public sealed class RecognitionResultWriter : IRecognitionResultWriter
{
    private readonly IRecognitionRepository _repository;
    private readonly ILogger<RecognitionResultWriter> _logger;

    public RecognitionResultWriter(
        IRecognitionRepository repository,
        ILogger<RecognitionResultWriter> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<RecognitionPersistenceResult> PersistAsync(
        RecognitionPersistenceRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Recognition result persistence started. SessionId={SessionId} FaceIndex={FaceIndex} CorrelationId={CorrelationId}",
            request.Context.AttendanceSessionId,
            request.Context.FaceIndex,
            request.Context.CorrelationId);

        return await _repository.PersistRecognitionAsync(request, cancellationToken);
    }
}
