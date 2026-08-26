using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3J (Architect package 3J1) —
/// Final Semester schema-hardening readiness GO/NO-GO contract. Read-only.
/// </summary>
public interface ISemesterSchemaHardeningReadinessService
{
    Task<SemesterSchemaHardeningReadinessResult> BuildAsync(CancellationToken cancellationToken = default);
}
