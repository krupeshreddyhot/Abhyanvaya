using Abhyanvaya.API.Hubs;
using Abhyanvaya.Application.DTOs.Faculty;
using Abhyanvaya.Application.Faculty;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.SignalR;

public sealed class FacultySignalRPublisher : IFacultyScheduleNotifier
{
    private readonly IHubContext<FacultyHub> _hub;

    public FacultySignalRPublisher(IHubContext<FacultyHub> hub) => _hub = hub;

    public Task PublishAsync(
        int tenantId,
        int? staffId,
        FacultyScheduleNotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>
        {
            _hub.Clients.Group(FacultySignalRGroups.Tenant(tenantId))
                .SendAsync("FacultyScheduleNotification", notification, cancellationToken)
        };
        if (staffId is > 0)
        {
            tasks.Add(_hub.Clients.Group(FacultySignalRGroups.Staff(staffId.Value))
                .SendAsync("FacultyScheduleNotification", notification, cancellationToken));
        }

        return Task.WhenAll(tasks);
    }
}
