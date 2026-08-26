using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3D — read-only legacy finalization & DB hardening discovery audit.</summary>
public interface ILegacySemesterFinalizationAuditService
{
    Task<LegacySemesterFinalizationAuditDto> BuildAuditAsync(CancellationToken cancellationToken = default);
}
