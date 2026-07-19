namespace Abhyanvaya.Application.EnrollmentApi;

public enum EnrollmentAuthorizationDecision
{
    Allowed = 0,
    Forbidden = 1,
    ContextRequired = 2,
}

public sealed record EnrollmentAuthorizationResult
{
    public required EnrollmentAuthorizationDecision Decision { get; init; }
    public string? FailureReason { get; init; }
    public int? TenantId { get; init; }
    public Guid? BatchId { get; init; }

    public bool IsAllowed => Decision == EnrollmentAuthorizationDecision.Allowed;

    public static EnrollmentAuthorizationResult Allowed(int tenantId, Guid? batchId = null) =>
        new()
        {
            Decision = EnrollmentAuthorizationDecision.Allowed,
            TenantId = tenantId,
            BatchId = batchId,
        };

    public static EnrollmentAuthorizationResult Forbidden(string reason = "Access denied.") =>
        new()
        {
            Decision = EnrollmentAuthorizationDecision.Forbidden,
            FailureReason = reason,
        };

    public static EnrollmentAuthorizationResult ContextRequired(string reason = "A college context is required for this operation.") =>
        new()
        {
            Decision = EnrollmentAuthorizationDecision.ContextRequired,
            FailureReason = reason,
        };
}
