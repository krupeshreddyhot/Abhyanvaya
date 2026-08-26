using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Exceptions;

/// <summary>
/// AI-SCHED-CAP Prompt 7 — Thrown when PublishAsync is blocked by publish readiness.
/// Carries the authoritative <see cref="TimetablePublishReadinessResultDto"/> for structured API rejection.
/// Does not imply any publish mutation occurred.
/// </summary>
public sealed class PublishNotReadyException : Exception
{
    public PublishNotReadyException(TimetablePublishReadinessResultDto readiness)
        : base(BuildMessage(readiness))
    {
        Readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
    }

    public TimetablePublishReadinessResultDto Readiness { get; }

    private static string BuildMessage(TimetablePublishReadinessResultDto readiness)
    {
        var blocking = readiness.BlockingFindingCount;
        return blocking <= 0
            ? "Timetable is not ready to publish."
            : $"Timetable is not ready to publish ({blocking} blocking finding(s)).";
    }
}
