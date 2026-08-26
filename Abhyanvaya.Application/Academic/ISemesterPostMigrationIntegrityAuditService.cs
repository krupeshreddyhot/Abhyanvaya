using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3B-A — read-only post-migration integrity audit.</summary>
public interface ISemesterPostMigrationIntegrityAuditService
{
    Task<SemesterPostMigrationIntegrityAuditDto> BuildAuditAsync(CancellationToken cancellationToken = default);
}
