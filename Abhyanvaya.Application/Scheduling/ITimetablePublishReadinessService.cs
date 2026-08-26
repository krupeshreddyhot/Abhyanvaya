using Abhyanvaya.Application.DTOs.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

/// <summary>
/// AI-SCHED-CAP Prompt 6 — Read-only Publish Readiness orchestration (Prompt 5 contract).
/// Observational only (no mutation). Prompt 7 PublishAsync consumes this as the publish gate.
/// </summary>
public interface ITimetablePublishReadinessService
{
    Task<TimetablePublishReadinessResultDto> EvaluatePublishReadinessAsync(
        int timetableId,
        CancellationToken cancellationToken = default);
}
