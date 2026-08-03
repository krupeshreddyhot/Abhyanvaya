using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Abhyanvaya.API.Hubs;

[Authorize]
public sealed class FacultyHub : Hub
{
    public Task SubscribeTenant(int tenantId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, FacultySignalRGroups.Tenant(tenantId));

    public Task SubscribeStaff(int staffId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, FacultySignalRGroups.Staff(staffId));
}

public static class FacultySignalRGroups
{
    public static string Tenant(int tenantId) => $"faculty-tenant:{tenantId}";
    public static string Staff(int staffId) => $"faculty-staff:{staffId}";
}
