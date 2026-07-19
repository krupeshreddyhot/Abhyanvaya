using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.TenantContext;

public sealed record TenantContextSnapshot
{
    public required int UserId { get; init; }
    public required string Role { get; init; }
    public int? SelectedCollegeId { get; init; }
    public string? SelectedCollegeName { get; init; }
    public string? SelectedCollegeCode { get; init; }
    public required int TenantId { get; init; }
    public required ContextType ContextType { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required bool IsGlobal { get; init; }
    public required string ContextSource { get; init; }
}

public sealed record TenantContextValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyList<string> Errors { get; init; }
    public string? ErrorCode { get; init; }

    public static TenantContextValidationResult Success() =>
        new() { IsValid = true, Errors = Array.Empty<string>() };

    public static TenantContextValidationResult Failure(string errorCode, params string[] errors) =>
        new() { IsValid = false, ErrorCode = errorCode, Errors = errors };
}

public sealed record TenantContextResolution
{
    public required bool IsResolved { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public TenantContextSnapshot? Context { get; init; }
    public int EffectiveTenantId { get; init; }
    public int? CollegeId { get; init; }
    public int UserId { get; init; }

    public static TenantContextResolution ContextRequired(string message = "A college context is required for this operation.") =>
        new()
        {
            IsResolved = false,
            ErrorCode = "ContextRequired",
            Message = message,
        };

    public static TenantContextResolution FromContext(TenantContextSnapshot context) =>
        new()
        {
            IsResolved = true,
            Context = context,
            EffectiveTenantId = context.TenantId,
            CollegeId = context.SelectedCollegeId,
            UserId = context.UserId,
        };
}

public sealed record SetCollegeContextRequest
{
    public required int CollegeId { get; init; }
}

public sealed record AvailableCollegeDto
{
    public required int Id { get; init; }
    public required int TenantId { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public string? ShortName { get; init; }
    public required string Status { get; init; }
    public required bool AiEnabled { get; init; }
    public string? UniversityName { get; init; }
}

public sealed record AvailableCollegesQuery
{
    public string? Search { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record PagedCollegesResult
{
    public required IReadOnlyList<AvailableCollegeDto> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

/// <summary>
/// Future hierarchy expansion seam (AI22.5 Phase 6). Not implemented — college scope only today.
/// </summary>
public interface ITenantContextHierarchy
{
    ContextType SupportedScope { get; }
    Task<TenantContextValidationResult> ValidateScopeAsync(int entityId, CancellationToken cancellationToken = default);
}
