using Abhyanvaya.Application.TenantContext;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Platform operational tenant context — independent of JWT claims.
/// SuperAdmin selects a college context; college admins inherit JWT tenant scope.
/// </summary>
public interface ITenantContextService
{
    Task<TenantContextSnapshot?> GetCurrentContextAsync(CancellationToken cancellationToken = default);

    Task<TenantContextValidationResult> SetCurrentContextAsync(int collegeId, CancellationToken cancellationToken = default);

    Task ClearContextAsync(CancellationToken cancellationToken = default);

    Task<TenantContextValidationResult> ValidateContextAsync(CancellationToken cancellationToken = default);

    bool IsGlobalContext();

    bool IsCollegeContext();

    TenantContextResolution ResolveForOperation();

    /// <summary>
    /// Binds the effective tenant to <see cref="ITenantContextAccessor"/> for the current HTTP scope.
    /// </summary>
    Task ApplyOperationalTenantAsync(CancellationToken cancellationToken = default);
}

public interface ITenantContextStore
{
    Task<TenantContextSnapshot?> GetAsync(int userId, CancellationToken cancellationToken = default);

    Task SetAsync(int userId, TenantContextSnapshot context, CancellationToken cancellationToken = default);

    Task RemoveAsync(int userId, CancellationToken cancellationToken = default);
}

public interface ITenantContextCollegeCatalog
{
    Task<PagedCollegesResult> GetAccessibleCollegesAsync(
        int userId,
        string role,
        int jwtTenantId,
        AvailableCollegesQuery query,
        CancellationToken cancellationToken = default);

    Task<TenantContextValidationResult> ValidateCollegeSelectionAsync(
        int collegeId,
        int userId,
        string role,
        int jwtTenantId,
        CancellationToken cancellationToken = default);
}
