using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H —
/// Read-only post–Prompt 3G integrity audit and schema-hardening readiness.
/// </summary>
public interface IPrompt3HPostSectionIntegrityAuditService
{
    Task<Prompt3HPostSectionIntegrityAuditDto> BuildAuditAsync(CancellationToken cancellationToken = default);
}
