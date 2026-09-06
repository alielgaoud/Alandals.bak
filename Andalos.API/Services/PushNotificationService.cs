using Andalos.API.Data;
using Lib.Net.Http.WebPush;
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

        public PushNotificationService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _pushClient = new PushServiceClient();

            var subject = config["VapidDetails:Subject"];
            var publicKey = config["VapidDetails:PublicKey"];
            var privateKey = config["VapidDetails:PrivateKey"];

            if (!string.IsNullOrEmpty(subject) && !string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(privateKey))
            {
                _pushClient.DefaultAuthentication = new Lib.Net.Http.WebPush.Authentication.VapidAuthentication(publicKey, privateKey)
                {
                    Subject = subject
                };
            }
        }

        public async Task SendPushNotificationAsync(int? userId, int? tenantId, string title, string body, string? url)
        {
            var query = _db.PushSubscriptions.Where(p => p.IsActive);

            if (userId.HasValue) query = query.Where(p => p.UserId == userId);
            else if (tenantId.HasValue) query = query.Where(p => p.TenantId == tenantId);
            else return;

            var subscriptions = await query.ToListAsync();
            if (!subscriptions.Any()) return;

            // بناء الهيكل القياسي الذي سيفهمه Service Worker في Angular
            var payload = JsonSerializer.Serialize(new
            {
                notification = new
                {
                    title = title,
                    body = body,
                    icon = "/assets/icons/icon-192x192.png", // أيقونة تطبيقك
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
                }
                catch (PushServiceClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // المتصفح ألغى الاشتراك أو حذفه، نقوم بتنظيف قاعدة البيانات
                    sub.IsActive = false;
                }
                catch (Exception)
                {
                    // تجاهل الأخطاء الأخرى للاستمرار في إرسال الباقي
                }
            }

            await _db.SaveChangesAsync();
        }
    }
}