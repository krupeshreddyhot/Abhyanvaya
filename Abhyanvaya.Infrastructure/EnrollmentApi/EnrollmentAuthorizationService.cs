using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.EnrollmentApi;

public sealed class EnrollmentAuthorizationService : IEnrollmentAuthorizationService
{
    private const string AccessDeniedMessage = "Access denied.";

    private readonly ITenantContextService _tenantContextService;
    private readonly IStudentEnrollmentBatchRepository _batchRepository;
    private readonly IEnrollmentActorPermissions _permissions;
    private readonly IEnrollmentAuthorizationTelemetry _telemetry;
    private readonly IAuditService _auditService;
    private readonly ILogger<EnrollmentAuthorizationService> _logger;

    public EnrollmentAuthorizationService(
        ITenantContextService tenantContextService,
        IStudentEnrollmentBatchRepository batchRepository,
        IEnrollmentActorPermissions permissions,
        IEnrollmentAuthorizationTelemetry telemetry,
        IAuditService auditService,
        ILogger<EnrollmentAuthorizationService> logger)
    {
        _tenantContextService = tenantContextService;
        _batchRepository = batchRepository;
        _permissions = permissions;
        _telemetry = telemetry;
        _auditService = auditService;
        _logger = logger;
    }

    public Task<EnrollmentAuthorizationResult> ValidateEnrollmentAccessAsync(CancellationToken cancellationToken = default) =>
        ValidateTenantAccessAsync(cancellationToken);

    public Task<EnrollmentAuthorizationResult> ValidateTenantAccessAsync(CancellationToken cancellationToken = default)
    {
        var resolution = _tenantContextService.ResolveForOperation();
        if (!resolution.IsResolved)
        {
            RecordViolation("ValidateTenantAccess", null, resolution.EffectiveTenantId, resolution.Message ?? "Context required.");
            return Task.FromResult(EnrollmentAuthorizationResult.ContextRequired(resolution.Message ?? "A college context is required for this operation."));
        }

        return Task.FromResult(EnrollmentAuthorizationResult.Allowed(resolution.EffectiveTenantId));
    }

    public Task<EnrollmentAuthorizationResult> ValidateBatchOwnershipAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        ValidateBatchAccessAsync(batchId, cancellationToken);

    public Task<EnrollmentAuthorizationResult> ValidateBatchAccessAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        ResolveBatchAccessAsync(batchId, "ValidateBatchAccess", cancellationToken);

    public Task<EnrollmentAuthorizationResult> CanViewBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        RequireViewPermissionAsync(batchId, "CanViewBatch", cancellationToken);

    public Task<EnrollmentAuthorizationResult> CanSubscribeBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        RequireViewPermissionAsync(batchId, "CanSubscribeBatch", cancellationToken);

    public Task<EnrollmentAuthorizationResult> CanCancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        RequireManagePermissionAsync(batchId, "CanCancelBatch", cancellationToken);

    public Task<EnrollmentAuthorizationResult> CanRetryBatchAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        RequireManagePermissionAsync(batchId, "CanRetryBatch", cancellationToken);

    private async Task<EnrollmentAuthorizationResult> RequireViewPermissionAsync(
        Guid batchId,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!_permissions.CanViewEnrollment)
        {
            RecordViolation(operation, batchId, null, AccessDeniedMessage);
            return EnrollmentAuthorizationResult.Forbidden();
        }

        return await ResolveBatchAccessAsync(batchId, operation, cancellationToken);
    }

    private async Task<EnrollmentAuthorizationResult> RequireManagePermissionAsync(
        Guid batchId,
        string operation,
        CancellationToken cancellationToken)
    {
        if (!_permissions.CanManageEnrollment)
        {
            RecordViolation(operation, batchId, null, AccessDeniedMessage);
            return EnrollmentAuthorizationResult.Forbidden();
        }

        return await ResolveBatchAccessAsync(batchId, operation, cancellationToken);
    }

    private async Task<EnrollmentAuthorizationResult> ResolveBatchAccessAsync(
        Guid batchId,
        string operation,
        CancellationToken cancellationToken)
    {
        var resolution = _tenantContextService.ResolveForOperation();
        if (!resolution.IsResolved)
        {
            RecordViolation(operation, batchId, resolution.EffectiveTenantId, resolution.Message ?? "Context required.");
            return EnrollmentAuthorizationResult.ContextRequired(resolution.Message ?? "A college context is required for this operation.");
        }

        var tenantId = resolution.EffectiveTenantId;
        var batch = await _batchRepository.GetBatchAsync(batchId, tenantId, cancellationToken);

        if (batch is null)
        {
            RecordViolation(operation, batchId, tenantId, AccessDeniedMessage);
            _telemetry.RecordSubscriptionFailure(batchId, AccessDeniedMessage);
            return EnrollmentAuthorizationResult.Forbidden();
        }

        return EnrollmentAuthorizationResult.Allowed(tenantId, batchId);
    }

    private void RecordViolation(string operation, Guid? batchId, int? tenantId, string reason)
    {
        _telemetry.RecordUnauthorizedAttempt(operation, batchId, tenantId, reason);
        _logger.LogWarning(
            "Enrollment authorization denied operation={Operation} batchId={BatchId} tenantId={TenantId}",
            operation,
            batchId,
            tenantId);

        _ = _auditService.RecordAsync(
            "EnrollmentAuthorization",
            batchId?.ToString() ?? "tenant",
            Domain.Enums.AuditAction.Custom,
            newValues: new { operation, tenantId, reason, action = "AccessDenied" });
    }
}

public sealed class EnrollmentAuthorizationTelemetry : IEnrollmentAuthorizationTelemetry
{
    private readonly ILogger<EnrollmentAuthorizationTelemetry> _logger;

    public EnrollmentAuthorizationTelemetry(ILogger<EnrollmentAuthorizationTelemetry> logger)
    {
        _logger = logger;
    }

    public void RecordUnauthorizedAttempt(string operation, Guid? batchId, int? tenantId, string reason) =>
        _logger.LogWarning(
            "Enrollment unauthorized attempt operation={Operation} batchId={BatchId} tenantId={TenantId} reason={Reason}",
            operation,
            batchId,
            tenantId,
            reason);

    public void RecordSubscriptionFailure(Guid batchId, string reason) =>
        _logger.LogWarning("Enrollment SignalR subscription failed batchId={BatchId} reason={Reason}", batchId, reason);

    public void RecordTenantViolation(string operation, int? attemptedTenantId, int? resolvedTenantId) =>
        _logger.LogWarning(
            "Enrollment tenant violation operation={Operation} attemptedTenantId={AttemptedTenantId} resolvedTenantId={ResolvedTenantId}",
            operation,
            attemptedTenantId,
            resolvedTenantId);
}
