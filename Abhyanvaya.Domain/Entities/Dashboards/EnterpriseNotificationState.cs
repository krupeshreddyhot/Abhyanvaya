using Abhyanvaya.Domain.Common;

namespace Abhyanvaya.Domain.Entities.Dashboards;

/// <summary>
/// AI31.6.7 — user state for composed notification items (pin / dismiss / archive / unread).
/// Notification payloads are composed from existing sources; only state is persisted here.
/// </summary>
public class EnterpriseNotificationState : BaseEntity
{
    public int UserId { get; set; }
    public string NotificationId { get; set; } = "";
    public bool IsRead { get; set; }
    public bool IsPinned { get; set; }
    public bool IsDismissed { get; set; }
    public bool IsArchived { get; set; }
}
