using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class AttendanceResultWriter : IAttendanceResultWriter
{
    private readonly IAttendanceRecognitionRepository _repository;
    private readonly IAttendanceSessionSummaryService _summaryService;
    private readonly ILogger<AttendanceResultWriter> _logger;

    public AttendanceResultWriter(
        IAttendanceRecognitionRepository repository,
        IAttendanceSessionSummaryService summaryService,
        ILogger<AttendanceResultWriter> logger)
    {
        _repository = repository;
        _summaryService = summaryService;
        _logger = logger;
    }

    public async Task<AttendancePersistenceResult> PersistAsync(
        AttendancePersistenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = request.Context.Session.SessionId;

        _logger.LogInformation(
            "Attendance persistence started. SessionId={SessionId} DecisionCount={DecisionCount} CorrelationId={CorrelationId}",
            sessionId,
            request.Decisions.Count,
            request.Context.CorrelationId);

        var persisted = await _repository.ApplyAttendanceDecisionsAsync(request.Decisions, cancellationToken);
        await _summaryService.SyncSessionSummaryAsync(sessionId, cancellationToken);

        return new AttendancePersistenceResult
        {
            Success = true,
            DecisionsPersisted = persisted,
        };
    }
}
