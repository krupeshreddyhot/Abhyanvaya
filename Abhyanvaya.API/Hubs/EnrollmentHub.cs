using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.Hubs;

[Authorize]
public sealed class EnrollmentHub : Hub
{
    private readonly IEnrollmentAuthorizationService _authorizationService;
    private readonly IBatchCancellationService _cancellationService;
    private readonly IBatchRetryService _retryService;
    private readonly ITenantContextService _tenantContextService;
    private readonly ILogger<EnrollmentHub> _logger;

    public EnrollmentHub(
        IEnrollmentAuthorizationService authorizationService,
        IBatchCancellationService cancellationService,
        IBatchRetryService retryService,
        ITenantContextService tenantContextService,
        ILogger<EnrollmentHub> logger)
    {
        _authorizationService = authorizationService;
        _cancellationService = cancellationService;
        _retryService = retryService;
        _tenantContextService = tenantContextService;
        _logger = logger;
    }

    public async Task SubscribeTenant()
    {
        var authorization = await _authorizationService.ValidateTenantAccessAsync(Context.ConnectionAborted);
        await EnsureAllowedAsync(authorization);

        await Groups.AddToGroupAsync(Context.ConnectionId, EnrollmentSignalRGroups.Tenant(authorization.TenantId!.Value));
        _logger.LogInformation("Enrollment tenant subscription user={UserId} tenantId={TenantId}", Context.UserIdentifier, authorization.TenantId);
    }

    public async Task SubscribeBatch(Guid batchId)
    {
        var authorization = await _authorizationService.CanSubscribeBatchAsync(batchId, Context.ConnectionAborted);
        await EnsureAllowedAsync(authorization);

        await Groups.AddToGroupAsync(Context.ConnectionId, EnrollmentSignalRGroups.Batch(batchId));
        await Groups.AddToGroupAsync(Context.ConnectionId, EnrollmentSignalRGroups.Tenant(authorization.TenantId!.Value));
        _logger.LogInformation("Enrollment batch subscription user={UserId} batchId={BatchId} tenantId={TenantId}", Context.UserIdentifier, batchId, authorization.TenantId);
    }

    public async Task CancelBatch(Guid batchId)
    {
        var authorization = await _authorizationService.CanCancelBatchAsync(batchId, Context.ConnectionAborted);
        await EnsureAllowedAsync(authorization);

        var resolution = _tenantContextService.ResolveForOperation();
        await _cancellationService.CancelAsync(batchId, resolution.EffectiveTenantId, resolution.UserId, Context.ConnectionAborted);
    }

    public async Task RetryBatch(Guid batchId)
    {
        var authorization = await _authorizationService.CanRetryBatchAsync(batchId, Context.ConnectionAborted);
        await EnsureAllowedAsync(authorization);

        var resolution = _tenantContextService.ResolveForOperation();
        await _retryService.RetryAsync(batchId, resolution.EffectiveTenantId, resolution.UserId, Context.ConnectionAborted);
    }

    private static Task EnsureAllowedAsync(EnrollmentAuthorizationResult authorization)
    {
        if (authorization.IsAllowed)
        {
            return Task.CompletedTask;
        }

        throw authorization.Decision switch
        {
            EnrollmentAuthorizationDecision.ContextRequired => new HubException(authorization.FailureReason ?? "A college context is required for this operation."),
            _ => new HubException(authorization.FailureReason ?? "Access denied."),
        };
    }
}
