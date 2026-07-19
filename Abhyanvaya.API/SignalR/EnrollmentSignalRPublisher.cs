using Abhyanvaya.Application.EnrollmentApi;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.SignalR;

public sealed class EnrollmentSignalRPublisher : IEnrollmentSignalRPublisher
{
    private readonly IHubContext<EnrollmentHub> _hubContext;

    public EnrollmentSignalRPublisher(IHubContext<EnrollmentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishBatchCreatedAsync(int tenantId, Guid batchId, int totalStudents, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(EnrollmentSignalRGroups.Tenant(tenantId))
            .SendAsync("BatchCreated", new { batchId, totalStudents, tenantId }, cancellationToken);

    public Task PublishBatchStartedAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(EnrollmentSignalRGroups.Batch(batchId))
            .SendAsync("BatchStarted", new { batchId, tenantId }, cancellationToken);

    public Task PublishProgressAsync(int tenantId, BatchProgressDto progress, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(EnrollmentSignalRGroups.Batch(progress.BatchId))
            .SendAsync("BatchProgress", progress, cancellationToken);

    public Task PublishCompletedAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default) =>
        PublishBatchLifecycleAsync(tenantId, batchId, "BatchCompleted", cancellationToken);

    public Task PublishCancelledAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default) =>
        PublishBatchLifecycleAsync(tenantId, batchId, "BatchCancelled", cancellationToken);

    public Task PublishFailedAsync(int tenantId, Guid batchId, string reason, CancellationToken cancellationToken = default) =>
        Task.WhenAll(
            _hubContext.Clients.Group(EnrollmentSignalRGroups.Batch(batchId))
                .SendAsync("BatchFailed", new { batchId, tenantId, reason }, cancellationToken),
            _hubContext.Clients.Group(EnrollmentSignalRGroups.Tenant(tenantId))
                .SendAsync("BatchFailed", new { batchId, tenantId, reason }, cancellationToken));

    public Task PublishDashboardChangedAsync(int tenantId, object dashboardSummary, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(EnrollmentSignalRGroups.Tenant(tenantId))
            .SendAsync("DashboardChanged", dashboardSummary, cancellationToken);

    public Task PublishRecoveryAsync(int tenantId, object recoveryEvent, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(EnrollmentSignalRGroups.Tenant(tenantId))
            .SendAsync("RecoveryEvent", recoveryEvent, cancellationToken);

    private Task PublishBatchLifecycleAsync(int tenantId, Guid batchId, string eventName, CancellationToken cancellationToken) =>
        Task.WhenAll(
            _hubContext.Clients.Group(EnrollmentSignalRGroups.Batch(batchId))
                .SendAsync(eventName, new { batchId, tenantId }, cancellationToken),
            _hubContext.Clients.Group(EnrollmentSignalRGroups.Tenant(tenantId))
                .SendAsync(eventName, new { batchId, tenantId }, cancellationToken));
}

public sealed class NoOpEnrollmentSignalRPublisher : IEnrollmentSignalRPublisher
{
    public Task PublishBatchCreatedAsync(int tenantId, Guid batchId, int totalStudents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishBatchStartedAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishProgressAsync(int tenantId, BatchProgressDto progress, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishCompletedAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishCancelledAsync(int tenantId, Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishFailedAsync(int tenantId, Guid batchId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishDashboardChangedAsync(int tenantId, object dashboardSummary, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishRecoveryAsync(int tenantId, object recoveryEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
