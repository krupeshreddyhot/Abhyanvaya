namespace Abhyanvaya.API.ProblemDetails;

/// <summary>
/// Stable RFC7807 type URIs and error codes for Abhyanvaya API responses.
/// </summary>
public static class ApiProblemTypes
{
    public const string Validation = "https://abhyanvaya.dev/problems/validation";
    public const string NotFound = "https://abhyanvaya.dev/problems/not-found";
    public const string Unauthorized = "https://abhyanvaya.dev/problems/unauthorized";
    public const string DomainRule = "https://abhyanvaya.dev/problems/domain-rule";
    public const string ConcurrencyConflict = "https://abhyanvaya.dev/problems/concurrency-conflict";
    public const string Database = "https://abhyanvaya.dev/problems/database";
    public const string Internal = "https://abhyanvaya.dev/problems/internal";
}

/// <summary>
/// Machine-readable error codes returned in ProblemDetails extensions.
/// </summary>
public static class ApiErrorCodes
{
    public const string ValidationFailed = "ValidationFailed";
    public const string NotFound = "NotFound";
    public const string Unauthorized = "Unauthorized";
    public const string DomainRuleViolation = "DomainRuleViolation";
    public const string ConcurrencyConflict = "ConcurrencyConflict";
    public const string DatabaseError = "DatabaseError";
    public const string InternalError = "InternalError";
}

/// <summary>
/// Extension keys applied to RFC7807 ProblemDetails payloads.
/// </summary>
public static class ApiProblemExtensions
{
    public const string ErrorCode = "errorCode";
    public const string TraceId = "traceId";
    public const string ReloadRequired = "reloadRequired";
    public const string ValidationErrors = "validationErrors";
    public const string Details = "details";
}
