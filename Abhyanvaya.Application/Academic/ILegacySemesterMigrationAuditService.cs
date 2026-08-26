using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 2B — read-only legacy Semester migration audit.</summary>
public interface ILegacySemesterMigrationAuditService
{
    /// <summary>Produces a fail-closed mapping worksheet. Does not mutate any data.</summary>
    Task<LegacySemesterMigrationAuditReportDto> BuildAuditAsync(CancellationToken cancellationToken = default);
}
