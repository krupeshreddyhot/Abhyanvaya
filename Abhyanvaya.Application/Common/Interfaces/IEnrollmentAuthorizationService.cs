using Abhyanvaya.Application.EnrollmentApi;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IEnrollmentAuthorizationService
{
    Task<EnrollmentAuthorizationResult> ValidateBatchAccessAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> ValidateEnrollmentAccessAsync(CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> ValidateTenantAccessAsync(CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> ValidateBatchOwnershipAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> CanViewBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> CanSubscribeBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> CanCancelBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    Task<EnrollmentAuthorizationResult> CanRetryBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
}

public interface IEnrollmentActorPermissions
{
    bool CanViewEnrollment { get; }

    bool CanManageEnrollment { get; }
}

public interface IEnrollmentAuthorizationTelemetry
{
    void RecordUnauthorizedAttempt(string operation, Guid? batchId, int? tenantId, string reason);

    void RecordSubscriptionFailure(Guid batchId, string reason);

    void RecordTenantViolation(string operation, int? attemptedTenantId, int? resolvedTenantId);
}
