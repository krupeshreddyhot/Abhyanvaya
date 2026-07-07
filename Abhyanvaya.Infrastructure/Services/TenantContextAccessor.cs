using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Services
{
    /// <summary>
    /// Scoped, transport-agnostic tenant context. Holds the tenant for the current logical scope so
    /// EF Core global query filters resolve correctly outside of HTTP requests (background workers,
    /// message consumers, schedulers).
    /// </summary>
    /// <remarks>
    /// State is confined to the instance (no static state, no <c>AsyncLocal</c>) and is registered
    /// with a scoped lifetime, so each unit of work owns its own tenant binding. A lock guards the
    /// backing field so that async continuations resuming on different threads observe a consistent
    /// value.
    /// </remarks>
    public sealed class TenantContextAccessor : ITenantContextAccessor
    {
        private readonly object _gate = new();
        private int? _currentTenantId;

        public int? CurrentTenantId
        {
            get
            {
                lock (_gate)
                {
                    return _currentTenantId;
                }
            }
        }

        public void SetTenant(int tenantId)
        {
            lock (_gate)
            {
                _currentTenantId = tenantId;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _currentTenantId = null;
            }
        }
    }
}
