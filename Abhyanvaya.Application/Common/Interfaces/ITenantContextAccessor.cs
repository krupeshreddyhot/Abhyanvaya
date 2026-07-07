namespace Abhyanvaya.Application.Common.Interfaces
{
    /// <summary>
    /// Ambient, transport-agnostic tenant context for the current logical operation.
    /// </summary>
    /// <remarks>
    /// HTTP requests derive the tenant from the JWT via <see cref="ICurrentUserService"/> and do not
    /// need this accessor. Non-HTTP entry points (background workers, message consumers such as
    /// RabbitMQ / Azure Service Bus / Kafka, and schedulers such as Hangfire / Quartz.NET) have no
    /// <c>HttpContext</c>, so they establish the tenant explicitly through this accessor. The EF Core
    /// global tenant query filters then resolve exactly as they do for authenticated HTTP requests.
    /// Implementations are registered with a scoped lifetime; the tenant is bound per logical scope
    /// and must always be cleared in a <c>finally</c> block to prevent cross-job leakage.
    /// </remarks>
    public interface ITenantContextAccessor
    {
        /// <summary>The tenant that owns the current logical operation, or <see langword="null"/> when unset.</summary>
        int? CurrentTenantId { get; }

        /// <summary>Binds the current scope to the supplied tenant. Intended for non-HTTP entry points.</summary>
        void SetTenant(int tenantId);

        /// <summary>Removes any tenant binding for the current scope.</summary>
        void Clear();
    }
}
