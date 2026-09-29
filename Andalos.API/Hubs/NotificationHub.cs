using Andalos.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Andalos.API.Hubs;
[Authorize]
public sealed class NotificationHub(CurrentUser current) : Hub
{
    public static string UserGroup(int userId, string stamp, long version) => $"user_{userId}_{stamp}_{version}";
    public async Task RegisterAdmin(int userId)
    {
        var u = current.Required;
        if (!u.IsStaff || u.RequiresPasswordChange || userId != u.Id) throw new HubException("Access denied.");
        await JoinCurrentGroupAsync(u);
        await Clients.Caller.SendAsync("Connected", new { message = "Connected" });
    }
    public async Task RegisterTenant(int tenantId, int userId)
    {
        var u = current.Required;
        if (!u.IsTenant || u.RequiresPasswordChange || tenantId != u.TenantId || userId != u.Id) throw new HubException("Access denied.");
        // No client-selected tenant/user/group memberships and no role-wide broadcasts.
        await JoinCurrentGroupAsync(u);
        await Clients.Caller.SendAsync("Connected", new { message = "Connected" });
    }
    private async Task JoinCurrentGroupAsync(UserSnapshot user)
    {
        var group = UserGroup(user.Id, user.SecurityStamp, user.PermissionsVersion);
        if (Context.Items.TryGetValue("security.group", out var old) && old is string previous && previous != group)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous);
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        Context.Items["security.group"] = group;
    }
}
public sealed class LiveIdentityHubFilter(UserSnapshotReader reader, CurrentUser current) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocation.HubMethodName is not ("RegisterAdmin" or "RegisterTenant")) throw new HubException("Access denied.");
        if (invocation.Context.User is null || !await reader.ValidateAsync(invocation.Context.User, current, invocation.Context.ConnectionAborted))
        { invocation.Context.Abort(); throw new HubException("Invalid session."); }
        return await next(invocation);
    }
}
