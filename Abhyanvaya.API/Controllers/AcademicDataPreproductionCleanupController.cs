using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.DTOs.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI-SCHED-CATALOG/TIMETABLE P1-4 Prompt 3H (package 3HC1 / PromptCode P1-4-3HC1) —
/// Pre-production transactional reset (Attendance / Timetable / TeachingGroup) + Student Semester reconciliation.
/// </summary>
[ApiController]
[Route("api/academic-data/preproduction-cleanup")]
[Authorize(Policy = AuthorizationPolicies.CanManageSemesters)]
public sealed class AcademicDataPreproductionCleanupController : ControllerBase
{
    private readonly IPreProductionTransactionalResetService _reset;

    public AcademicDataPreproductionCleanupController(IPreProductionTransactionalResetService reset)
    {
        _reset = reset;
    }

    /// <summary>Read-only inventory, deletion allowlist counts, and Student Semester reconciliation plan.</summary>
    [HttpGet("preview")]
    public async Task<IActionResult> Preview(CancellationToken cancellationToken)
    {
        var report = await _reset.PreviewAsync(cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Controlled ALL_OR_NOTHING execute. Requires Confirm=true and
    /// ConfirmationPhrase=PREPRODUCTION_TRANSACTIONAL_RESET.
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> Execute(
        [FromBody] PreProductionTransactionalResetExecuteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reset.ExecuteAsync(request, cancellationToken);
        if (!result.IsSuccessful
            && !string.Equals(result.ExecutionStatus, "AlreadyComplete", StringComparison.OrdinalIgnoreCase))
            return Conflict(result);
        return Ok(result);
    }
}
