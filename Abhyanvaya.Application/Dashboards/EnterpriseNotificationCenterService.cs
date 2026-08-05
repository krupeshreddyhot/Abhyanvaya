using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Dashboards;
using Abhyanvaya.Application.Faculty;
using Abhyanvaya.Domain.Entities.Dashboards;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Dashboards;

public interface IEnterpriseNotificationCenterService
{
    Task<EnterpriseNotificationCenterDto> GetAsync(CancellationToken cancellationToken = default);
    Task<EnterpriseNotificationCenterDto> UpdateStateAsync(NotificationStateUpdateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// AI31.6.7 — composes Scheduling / Attendance / Recovery / System notifications.
/// SignalR-ready; no polling. Persists only user state (pin/dismiss/archive/read).
/// </summary>
public sealed class EnterpriseNotificationCenterService : IEnterpriseNotificationCenterService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFacultyDashboardService _facultyDashboard;
    private readonly IFacultySmartNotificationService _smartNotifications;

    public EnterpriseNotificationCenterService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFacultyDashboardService facultyDashboard,
        IFacultySmartNotificationService smartNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _facultyDashboard = facultyDashboard;
        _smartNotifications = smartNotifications;
    }

    public async Task<EnterpriseNotificationCenterDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var composed = await ComposeAsync(cancellationToken);
        List<EnterpriseNotificationState> states;
        try
        {
            states = await _db.EnterpriseNotificationStates
                .Where(s => s.TenantId == _currentUser.TenantId && s.UserId == _currentUser.UserId && !s.IsDeleted)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            // Schema not applied yet — still return composed notifications.
            states = [];
        }
        var byId = states.ToDictionary(s => s.NotificationId, StringComparer.OrdinalIgnoreCase);

        var items = composed
            .Select(n => ApplyState(n, byId.GetValueOrDefault(n.NotificationId)))
            .Where(n => !n.IsDismissed)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.OccurredUtc)
            .ToList();

        return new EnterpriseNotificationCenterDto
        {
            Items = items,
            UnreadCount = items.Count(i => i.IsUnread && !i.IsArchived)
        };
    }

    public async Task<EnterpriseNotificationCenterDto> UpdateStateAsync(
        NotificationStateUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NotificationId))
            throw new ArgumentException("NotificationId is required.", nameof(request));

        var entity = await _db.EnterpriseNotificationStates
            .FirstOrDefaultAsync(s =>
                s.TenantId == _currentUser.TenantId &&
                s.UserId == _currentUser.UserId &&
                s.NotificationId == request.NotificationId &&
                !s.IsDeleted, cancellationToken);

        if (entity is null)
        {
            entity = new EnterpriseNotificationState
            {
                TenantId = _currentUser.TenantId,
                UserId = _currentUser.UserId,
                NotificationId = request.NotificationId.Trim(),
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };
            await _db.AddAsync(entity);
        }

        if (request.IsRead.HasValue) entity.IsRead = request.IsRead.Value;
        if (request.IsPinned.HasValue) entity.IsPinned = request.IsPinned.Value;
        if (request.IsDismissed.HasValue) entity.IsDismissed = request.IsDismissed.Value;
        if (request.IsArchived.HasValue) entity.IsArchived = request.IsArchived.Value;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private async Task<List<EnterpriseNotificationItemDto>> ComposeAsync(CancellationToken cancellationToken)
    {
        var items = new List<EnterpriseNotificationItemDto>();

        try
        {
            var schedule = await _facultyDashboard.GetNotificationsAsync(cancellationToken);
            foreach (var n in schedule)
            {
                items.Add(new EnterpriseNotificationItemDto
                {
                    NotificationId = string.IsNullOrWhiteSpace(n.NotificationId)
                        ? $"sched-{n.Kind}-{n.OccurredUtc:O}"
                        : n.NotificationId,
                    Source = "Scheduling",
                    Category = n.Kind,
                    Priority = n.Kind is "Cancelled" or "FacultySubstitution" ? "High" : "Normal",
                    Title = n.Title,
                    Message = n.Message,
                    OccurredUtc = n.OccurredUtc,
                    Path = "/faculty"
                });
            }
        }
        catch
        {
            // Composition must remain resilient when faculty schedule feed is unavailable.
        }

        try
        {
            var smart = await _smartNotifications.GetSmartAsync(cancellationToken);
            foreach (var n in smart.Items)
            {
                items.Add(new EnterpriseNotificationItemDto
                {
                    NotificationId = string.IsNullOrWhiteSpace(n.NotificationId)
                        ? $"smart-{n.Kind}-{n.OccurredUtc:O}"
                        : $"smart-{n.NotificationId}",
                    Source = n.Kind.Contains("Ai", StringComparison.OrdinalIgnoreCase) ? "Attendance" : "System",
                    Category = n.Kind,
                    Priority = n.Kind.Contains("Reminder", StringComparison.OrdinalIgnoreCase) ? "High" : "Normal",
                    Title = n.Title,
                    Message = n.Message,
                    OccurredUtc = n.OccurredUtc,
                    Path = "/faculty"
                });
            }
        }
        catch
        {
            // Optional smart feed.
        }

        if (items.Count == 0)
        {
            items.Add(new EnterpriseNotificationItemDto
            {
                NotificationId = "system-ready",
                Source = "System",
                Category = "System",
                Priority = "Low",
                Title = "Notification Center ready",
                Message = "Live updates arrive via SignalR (FacultyHub). No polling.",
                OccurredUtc = DateTime.UtcNow,
                Path = "/dashboard/notifications"
            });
        }

        return items;
    }

    private static EnterpriseNotificationItemDto ApplyState(
        EnterpriseNotificationItemDto item,
        EnterpriseNotificationState? state)
    {
        if (state is null) return item;
        return new EnterpriseNotificationItemDto
        {
            NotificationId = item.NotificationId,
            Source = item.Source,
            Category = item.Category,
            Priority = item.Priority,
            Title = item.Title,
            Message = item.Message,
            OccurredUtc = item.OccurredUtc,
            Path = item.Path,
            IsUnread = !state.IsRead,
            IsPinned = state.IsPinned,
            IsDismissed = state.IsDismissed,
            IsArchived = state.IsArchived
        };
    }
}
