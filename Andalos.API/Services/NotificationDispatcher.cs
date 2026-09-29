using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Hubs;
using Andalos.API.Interfaces;
using Andalos.API.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
namespace Andalos.API.Services;
// Existing Notifications table doubles as a durable outbox. At-least-once delivery; clients deduplicate by notification ID.
public sealed class NotificationDispatcher(IServiceScopeFactory scopes, ILogger<NotificationDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var permissions = scope.ServiceProvider.GetRequiredService<EffectivePermissions>();
                var snapshots = scope.ServiceProvider.GetRequiredService<UserSnapshotReader>();
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();
                var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                var now = DateTimeHelper.LibyaNow;
                var rows = await db.Notifications.Where(n => n.IsActive && n.RealtimeDeliveredAt == null &&
                    (n.IsSent || (n.ScheduledFor.HasValue && n.ScheduledFor <= now))).OrderBy(n => n.Id).Take(50).ToListAsync(ct);
                foreach (var n in rows)
                {
                    if (!Enum.IsDefined(n.Type))
                    {
                        n.IsActive = false; // quarantine an unclassified legacy type, never permissionless broadcast
                        logger.LogWarning("Unclassified notification type quarantined for row {Id}.", n.Id);
                        await db.SaveChangesAsync(ct); continue;
                    }
                    var ids = await db.Users.Where(u => u.IsActive && !u.IsLocked && !u.RequiresPasswordChange &&
                        (n.UserId == u.Id && (u.Role != UserRole.Tenant && u.Role != UserRole.TenantStaff || n.TenantId == null || n.TenantId == u.TenantId) || (n.UserId == null && n.TenantId != null && u.TenantId == n.TenantId && (u.Role == UserRole.Tenant || u.Role == UserRole.TenantStaff)) ||
                        (n.UserId == null && n.TenantId == null && n.TargetGroup == "AllTenants" && (u.Role == UserRole.Tenant || u.Role == UserRole.TenantStaff)) ||
                        (n.UserId == null && n.TenantId == null && (n.TargetGroup == "Admins" || n.TargetGroup == "Accountants") &&
                            (u.Role == UserRole.SuperAdmin || u.Role == UserRole.Admin || u.Role == UserRole.Accountant || u.Role == UserRole.GateKeeper))))
                        .Select(u => u.Id).ToListAsync(ct);
                    foreach (var id in ids)
                    {
                        var u = await snapshots.ReadAsync(id, ct);
                        if (u is null || !u.IsActive || u.IsLocked || u.RequiresPasswordChange) continue;
                        if (u.IsStaff)
                        {
                            var key = NotificationPrivacy.StaffKey(n.Type);
                            if (key is not null && !await permissions.HasAsync(u, key, ct)) continue;
                        }
                        else if (!u.IsTenant) continue;
                        else if (u.Role == UserRole.TenantStaff && NotificationPrivacy.PortalCapability(n.Type) is string cap &&
                            !await db.TenantStaffPermissions.AnyAsync(p => p.UserId == id && p.Capability == cap, ct)) continue;
                        if (await service.IsNotificationEnabledAsync(id, u.IsTenant ? u.TenantId : null, n.Type, NotificationChannel.InApp))
                            await hub.Clients.Group(NotificationHub.UserGroup(id, u.SecurityStamp, u.PermissionsVersion)).SendAsync("ReceiveNotification", service.ToDto(n), ct);
                        if (await service.IsNotificationEnabledAsync(id, u.IsTenant ? u.TenantId : null, n.Type, NotificationChannel.Push))
                            await push.SendPushNotificationAsync(id, u.IsTenant ? u.TenantId : null, n.Title, n.Message, n.ActionUrl);
                    }
                    n.IsSent = true; n.SentAt ??= now; n.RealtimeDeliveredAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification outbox delivery failed; pending rows will be retried."); }
        }
    }
}
