using Abhyanvaya.Application.DTOs.Academic;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (package 3HC1 / PromptCode P1-4-3HC1) —
/// Pre-production transactional reset preview + execute.
/// </summary>
public interface IPreProductionTransactionalResetService
{
    Task<PreProductionTransactionalResetPreviewDto> PreviewAsync(CancellationToken cancellationToken = default);

    Task<PreProductionTransactionalResetExecuteResultDto> ExecuteAsync(
        PreProductionTransactionalResetExecuteRequest request,
        CancellationToken cancellationToken = default);
}
