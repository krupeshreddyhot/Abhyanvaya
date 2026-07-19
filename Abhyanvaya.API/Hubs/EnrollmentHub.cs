using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.EnrollmentApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.Hubs;

[Authorize]
public sealed class EnrollmentHub : Hub
{
    public Task SubscribeBatch(Guid batchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, BatchGroup(batchId));

    public static string BatchGroup(Guid batchId) => $"enrollment-batch:{batchId}";
}

public sealed class EnrollmentEventPublisher : IEnrollmentEventPublisher
{
    private readonly IHubContext<EnrollmentHub> _hubContext;

    public EnrollmentEventPublisher(IHubContext<EnrollmentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishBatchCreatedAsync(Guid batchId, int totalStudents, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.All.SendAsync("BatchCreated", new { batchId, totalStudents }, cancellationToken);

    public Task PublishBatchStartedAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(EnrollmentHub.BatchGroup(batchId)).SendAsync("BatchStarted", new { batchId }, cancellationToken);

    public Task PublishBatchProgressAsync(BatchProgressDto progress, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(EnrollmentHub.BatchGroup(progress.BatchId)).SendAsync("BatchProgress", progress, cancellationToken);

    public Task PublishBatchCompletedAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(EnrollmentHub.BatchGroup(batchId)).SendAsync("BatchCompleted", new { batchId }, cancellationToken);

    public Task PublishBatchFailedAsync(Guid batchId, string reason, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(EnrollmentHub.BatchGroup(batchId)).SendAsync("BatchFailed", new { batchId, reason }, cancellationToken);

    public Task PublishBatchCancelledAsync(Guid batchId, CancellationToken cancellationToken = default) =>
        _hubContext.Clients.Group(EnrollmentHub.BatchGroup(batchId)).SendAsync("BatchCancelled", new { batchId }, cancellationToken);
}

public sealed class NoOpEnrollmentEventPublisher : IEnrollmentEventPublisher
{
    public Task PublishBatchCreatedAsync(Guid batchId, int totalStudents, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishBatchStartedAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishBatchProgressAsync(BatchProgressDto progress, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishBatchCompletedAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishBatchFailedAsync(Guid batchId, string reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task PublishBatchCancelledAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
