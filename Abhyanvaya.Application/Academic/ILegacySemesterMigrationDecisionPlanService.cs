using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3A — read-only migration decision plan.</summary>
public interface ILegacySemesterMigrationDecisionPlanService
{
    Task<LegacySemesterMigrationDecisionPlanDto> BuildDecisionPlanAsync(CancellationToken cancellationToken = default);
}
