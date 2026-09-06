using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace Andalos.API.Hubs
{
    public class NotificationHub : Hub
    {
        // قاموس لتتبع اتصالات كل مستخدم (مستخدم واحد قد يفتح عدة أجهزة)
        private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();
        private static readonly ConcurrentDictionary<string, HashSet<string>> _tenantConnections = new();

        // ===== 1. عند اتصال مستخدم إداري =====
        public async Task RegisterAdmin(int userId)
        {
            var connectionId = Context.ConnectionId;
            var key = $"user_{userId}";

            _userConnections.AddOrUpdate(key,
                new HashSet<string> { connectionId },
                (_, connections) =>
                {
                    connections.Add(connectionId);
                    return connections;
                });

            // إضافة للمجموعة لاستقبال الإشعارات العامة
            await Groups.AddToGroupAsync(connectionId, "Admins");
            await Groups.AddToGroupAsync(connectionId, key);

            await Clients.Caller.SendAsync("Connected", new { message = "تم الاتصال بنجاح" });
        }

        // ===== 2. عند اتصال مستأجر =====
        public async Task RegisterTenant(int tenantId, int userId)
        {
            var connectionId = Context.ConnectionId;
            var tenantKey = $"tenant_{tenantId}";
            var userKey = $"user_{userId}";

            _tenantConnections.AddOrUpdate(tenantKey,
                new HashSet<string> { connectionId },
                (_, connections) =>
                {
                    connections.Add(connectionId);
                    return connections;
                });

            await Groups.AddToGroupAsync(connectionId, "AllTenants");
            await Groups.AddToGroupAsync(connectionId, tenantKey);
            await Groups.AddToGroupAsync(connectionId, userKey);

            await Clients.Caller.SendAsync("Connected", new { message = "تم الاتصال بنجاح" });
        }

        // ===== 3. عند قطع الاتصال =====
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            foreach (var kvp in _userConnections)
            {
                kvp.Value.Remove(connectionId);
                if (!kvp.Value.Any())
                    _userConnections.TryRemove(kvp.Key, out _);
            }

            foreach (var kvp in _tenantConnections)
            {
                kvp.Value.Remove(connectionId);
                if (!kvp.Value.Any())
                    _tenantConnections.TryRemove(kvp.Key, out _);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}