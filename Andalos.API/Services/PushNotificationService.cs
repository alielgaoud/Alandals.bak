using Andalos.API.Data;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Andalos.API.Services
{
    public interface IPushNotificationService
    {
        Task SendPushNotificationAsync(int? userId, int? tenantId, string title, string body, string? url);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly AppDbContext _db;
        private readonly PushServiceClient _pushClient;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly bool _isConfigured;

        public PushNotificationService(
            AppDbContext db,
            IConfiguration config,
            ILogger<PushNotificationService> logger)
        {
            _db = db;
            _logger = logger;
            _pushClient = new PushServiceClient();
            _isConfigured = false;

            var subject = config["VapidDetails:Subject"];
            var publicKey = config["VapidDetails:PublicKey"];
            var privateKey = config["VapidDetails:PrivateKey"];

            // 👈 لا نرمي Exception عند مفاتيح غير صالحة (مهم أثناء التطوير)
            if (string.IsNullOrWhiteSpace(subject) ||
                string.IsNullOrWhiteSpace(publicKey) ||
                string.IsNullOrWhiteSpace(privateKey) ||
                privateKey.Contains("YOUR_PRIVATE_KEY", StringComparison.OrdinalIgnoreCase) ||
                privateKey.Contains("generate_it_later", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("⚠️ VAPID Keys غير مهيأة. إشعارات Web Push معطلة مؤقتاً (In-App/SignalR تعمل).");
                return;
            }

            try
            {
                _pushClient.DefaultAuthentication = new VapidAuthentication(publicKey, privateKey)
                {
                    Subject = subject
                };
                _isConfigured = true;
                _logger.LogInformation("✅ تم تهيئة Web Push (VAPID) بنجاح.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ فشل تهيئة VAPID Keys. سيتم تعطيل Web Push مؤقتاً.");
                _isConfigured = false;
            }
        }

        public async Task SendPushNotificationAsync(int? userId, int? tenantId, string title, string body, string? url)
        {
            // إذا لم تُهيأ المفاتيح: نخرج بهدوء بدون تعطيل النظام
            if (!_isConfigured)
            {
                _logger.LogDebug("تم تجاهل Push Notification لأن VAPID غير مهيأ.");
                return;
            }

            var query = _db.PushSubscriptions.Where(p => p.IsActive);

            if (userId.HasValue)
                query = query.Where(p => p.UserId == userId);
            else if (tenantId.HasValue)
                query = query.Where(p => p.TenantId == tenantId);
            else
                return;

            var subscriptions = await query.ToListAsync();
            if (!subscriptions.Any()) return;

            var payload = JsonSerializer.Serialize(new
            {
                notification = new
                {
                    title,
                    body,
                    icon = "/assets/icons/icon-192x192.png",
                    vibrate = new[] { 100, 50, 100 },
                    data = new { url = url ?? "/" }
                }
            });

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSubscription = new PushSubscription
                    {
                        Endpoint = sub.Endpoint,
                        Keys = new Dictionary<string, string>
                        {
                            { "p256dh", sub.P256dh },
                            { "auth", sub.Auth }
                        }
                    };

                    var pushMessage = new PushMessage(payload);
                    await _pushClient.RequestPushMessageDeliveryAsync(pushSubscription, pushMessage);

                    sub.LastUsedAt = DateTime.UtcNow;
                }
                catch (PushServiceClientException ex)
                    when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                          ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // الاشتراك منتهٍ: نعطله
                    sub.IsActive = false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "فشل إرسال Push لاشتراك: {Endpoint}", sub.Endpoint);
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}