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
    private readonly IRecentContextService _recentContext;
    private readonly IContextRefreshService _contextRefresh;
    private readonly IContextDiagnosticsService _diagnostics;
    private readonly ICurrentUserService _currentUser;

    public ContextController(
        ITenantContextService tenantContextService,
        ITenantContextCollegeCatalog collegeCatalog,
        IRecentContextService recentContext,
        IContextRefreshService contextRefresh,
        IContextDiagnosticsService diagnostics,
        ICurrentUserService currentUser)
    {
        _tenantContextService = tenantContextService;
        _collegeCatalog = collegeCatalog;
        _recentContext = recentContext;
        _contextRefresh = contextRefresh;
        _diagnostics = diagnostics;
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

    /// <summary>Recent colleges for SuperAdmin quick selection (max 10, most recent first).</summary>
    [HttpGet("recent-colleges")]
    [ProducesResponseType(typeof(RecentCollegesResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecentCollegesResult>> GetRecentColleges(CancellationToken cancellationToken)
    {
        var recent = await _recentContext.GetRecentCollegesAsync(_currentUser.UserId, cancellationToken);
        IReadOnlyList<AvailableCollegeDto> popular = [];

        if (recent.Count == 0)
        {
            var catalog = await _collegeCatalog.GetAccessibleCollegesAsync(
                _currentUser.UserId,
                _currentUser.Role,
                _currentUser.TenantId,
                new AvailableCollegesQuery { Page = 1, PageSize = 5 },
                cancellationToken);
            popular = catalog.Items;
        }

        return Ok(new RecentCollegesResult { Recent = recent, Popular = popular });
    }

    /// <summary>Extends operational context TTL without changing JWT identity.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!IsSuperAdmin())
        {
            return Forbid();
        }

        var refreshed = await _contextRefresh.RefreshAsync(_currentUser.UserId, cancellationToken);
        if (!refreshed)
        {
            return BadRequest(new { errorCode = "NoContext", message = "No operational context to refresh." });
        }

        return NoContent();
    }

    /// <summary>Read-only operational context diagnostics for support engineers.</summary>
    [HttpGet("diagnostics")]
    [Authorize(Roles = nameof(Domain.Enums.UserRole.SuperAdmin))]
    [ProducesResponseType(typeof(ContextDiagnosticsReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContextDiagnosticsReport>> GetDiagnostics(CancellationToken cancellationToken)
    {
        var report = await _diagnostics.GetDiagnosticsAsync(_currentUser.UserId, cancellationToken);
        return Ok(report);
    }

    private bool IsSuperAdmin() =>
        string.Equals(_currentUser.Role, nameof(Domain.Enums.UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase);
}
