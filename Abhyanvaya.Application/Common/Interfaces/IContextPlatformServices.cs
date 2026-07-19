using Abhyanvaya.Application.TenantContext;

namespace Abhyanvaya.Application.Common.Interfaces;

public interface IRecentContextRepository
{
    Task<IReadOnlyList<RecentCollegeEntry>> GetRecentCollegesAsync(int userId, CancellationToken cancellationToken = default);

    Task SaveRecentCollegesAsync(int userId, IReadOnlyList<RecentCollegeEntry> entries, CancellationToken cancellationToken = default);
}

public interface IRecentContextService
{
    Task RecordCollegeSelectionAsync(int userId, AvailableCollegeDto college, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentCollegeEntry>> GetRecentCollegesAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IContextExpirationService
{
    TimeSpan DefaultTimeout { get; }

    DateTime ComputeExpiresUtc(DateTime createdUtc);

    bool IsExpired(TenantContextSnapshot snapshot);

    TimeSpan GetRemainingTime(TenantContextSnapshot snapshot);
}

public interface IContextRefreshService
{
    Task<bool> RefreshAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IContextCleanupWorker
{
    Task<int> CleanupExpiredContextAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IContextEventPublisher
{
    Task PublishContextChangedAsync(TenantContextSnapshot context, CancellationToken cancellationToken = default);

    Task PublishContextClearedAsync(int userId, CancellationToken cancellationToken = default);

    Task PublishContextExpiredAsync(int userId, CancellationToken cancellationToken = default);

    Task PublishContextRestoredAsync(TenantContextSnapshot context, CancellationToken cancellationToken = default);
}

public interface IContextEventSubscriber
{
    void OnContextChanged(Func<TenantContextSnapshot, Task> handler);

    void OnContextCleared(Func<int, Task> handler);

    void OnContextExpired(Func<int, Task> handler);

    void OnContextRestored(Func<TenantContextSnapshot, Task> handler);
}

public interface IContextDiagnosticsService
{
    Task<ContextDiagnosticsReport> GetDiagnosticsAsync(int userId, CancellationToken cancellationToken = default);
}

public interface IContextOperationalMetricsCollector
{
    void RecordContextSwitch(int tenantId);

    void RecordContextExpired();

    void RecordContextValidationFailed();

    void RecordContextDuration(TimeSpan duration);

    Task<ContextOperationalMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IContextArchitectureValidator
{
    Task<ContextArchitectureValidationReport> ValidateAsync(CancellationToken cancellationToken = default);
}
