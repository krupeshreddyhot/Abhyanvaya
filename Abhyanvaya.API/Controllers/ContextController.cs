using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.TenantContext;
using Abhyanvaya.API.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

[ApiController]
[Authorize]
[Route("api/context")]
public sealed class ContextController : ControllerBase
{
    private readonly ITenantContextService _tenantContextService;
    private readonly ITenantContextCollegeCatalog _collegeCatalog;
    private readonly ICurrentUserService _currentUser;

    public ContextController(
        ITenantContextService tenantContextService,
        ITenantContextCollegeCatalog collegeCatalog,
        ICurrentUserService currentUser)
    {
        _tenantContextService = tenantContextService;
        _collegeCatalog = collegeCatalog;
        _currentUser = currentUser;
    }

    /// <summary>Returns the current operational tenant context for the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantContextSnapshot), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantContextSnapshot>> GetCurrent(CancellationToken cancellationToken)
    {
        var context = await _tenantContextService.GetCurrentContextAsync(cancellationToken);
        return Ok(context);
    }

    /// <summary>SuperAdmin selects an operational college context (not persisted in JWT).</summary>
    [HttpPost("college")]
    [ProducesResponseType(typeof(TenantContextSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TenantContextSnapshot>> SetCollege(
        [FromBody] SetCollegeContextRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin())
        {
            return Forbid();
        }

        var validation = await _tenantContextService.SetCurrentContextAsync(request.CollegeId, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                errorCode = validation.ErrorCode ?? "ValidationFailed",
                errors = validation.Errors,
            });
        }

        var context = await _tenantContextService.GetCurrentContextAsync(cancellationToken);
        return Ok(context);
    }

    /// <summary>Clears SuperAdmin operational context and returns to global scope.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin())
        {
            return Forbid();
        }

        await _tenantContextService.ClearContextAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Searchable college catalog for context selection.</summary>
    [HttpGet("available-colleges")]
    [ProducesResponseType(typeof(PagedCollegesResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedCollegesResult>> GetAvailableColleges(
        [FromQuery] AvailableCollegesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _collegeCatalog.GetAccessibleCollegesAsync(
            _currentUser.UserId,
            _currentUser.Role,
            _currentUser.TenantId,
            query,
            cancellationToken);

        return Ok(result);
    }

    private bool IsSuperAdmin() =>
        string.Equals(_currentUser.Role, nameof(Domain.Enums.UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);
}
