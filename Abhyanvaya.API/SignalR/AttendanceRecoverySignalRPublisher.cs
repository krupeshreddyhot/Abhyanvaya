using Abhyanvaya.API.Hubs;
using Abhyanvaya.Application.AttendanceRecovery;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.SignalR;

/// <summary>AI22.8 — reuses FacultyHub SignalR groups; no polling; no new notification framework.</summary>
public sealed class AttendanceRecoverySignalRPublisher : IAttendanceRecoveryNotifier
{
    private readonly IHubContext<FacultyHub> _hub;

    public AttendanceRecoverySignalRPublisher(IHubContext<FacultyHub> hub) => _hub = hub;

    public Task NotifyAsync(
        int tenantId,
        int? staffId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            _hub.Clients.Group(FacultySignalRGroups.Tenant(tenantId))
                .SendAsync("AttendanceRecoveryNotification", new { eventName, payload }, cancellationToken)
        };
        if (staffId is > 0)
        {
            tasks.Add(_hub.Clients.Group(FacultySignalRGroups.Staff(staffId.Value))
                .SendAsync("AttendanceRecoveryNotification", new { eventName, payload }, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }
}
