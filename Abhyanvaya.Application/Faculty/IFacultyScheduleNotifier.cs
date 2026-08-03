using Abhyanvaya.Application.DTOs.Faculty;

namespace Abhyanvaya.Application.Faculty;

/// <summary>SignalR bridge for faculty schedule notifications. No polling.</summary>
public interface IFacultyScheduleNotifier
{
    Task PublishAsync(int tenantId, int? staffId, FacultyScheduleNotificationDto notification, CancellationToken cancellationToken = default);
}

public sealed class NoOpFacultyScheduleNotifier : IFacultyScheduleNotifier
{
    public Task PublishAsync(int tenantId, int? staffId, FacultyScheduleNotificationDto notification, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
