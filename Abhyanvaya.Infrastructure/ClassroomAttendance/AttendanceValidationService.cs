using Abhyanvaya.Application.ClassroomAttendance;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.ClassroomAttendance;

public sealed class AttendanceValidationService : IAttendanceValidationService
{
    private readonly ILogger<AttendanceValidationService> _logger;

    public AttendanceValidationService(ILogger<AttendanceValidationService> logger)
    {
        _logger = logger;
    }

    public AttendanceValidationResult Validate(AttendanceSessionContext context)
    {
        var errors = new List<string>();
        var outcomes = context.RecognitionOutcomes ?? Array.Empty<FaceRecognitionOutcome>();

        if (outcomes.Count == 0)
        {
            errors.Add("No recognition outcomes available for validation.");
        }

        var validOutcomes = new List<FaceRecognitionOutcome>();

        foreach (var outcome in outcomes)
        {
            if (outcome.RecognitionResult == null)
            {
                errors.Add($"Face {outcome.FaceIndex} has no recognition result.");
                continue;
            }

            if (!outcome.RecognitionResult.Success)
            {
                errors.Add($"Face {outcome.FaceIndex} recognition failed: {outcome.RecognitionResult.FailureReason}");
                continue;
            }

            validOutcomes.Add(outcome);
        }

        var policy = context.Policy;
        if (policy != null)
        {
            foreach (var outcome in validOutcomes)
            {
                var confidence = outcome.RecognitionResult?.Decision?.Confidence ?? 0;
                if (confidence < (decimal)policy.MinimumConfidence
                    && outcome.RecognitionResult?.Decision?.Status != RecognitionStatus.Unknown)
                {
                    errors.Add($"Face {outcome.FaceIndex} below minimum confidence policy.");
                }
            }
        }

        _logger.LogInformation(
            "Attendance validation completed. SessionId={SessionId} Valid={Valid} Invalid={Invalid} CorrelationId={CorrelationId}",
            context.Session.SessionId,
            validOutcomes.Count,
            errors.Count,
            context.CorrelationId);

        return new AttendanceValidationResult
        {
            IsValid = validOutcomes.Count > 0,
            Errors = errors.Count > 0 ? errors : null,
            ValidOutcomes = validOutcomes,
        };
    }
}
