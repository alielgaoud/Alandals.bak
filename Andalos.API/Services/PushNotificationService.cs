using Andalos.API.Security;
using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
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
        private readonly IConfiguration _configuration;
        private readonly ISettingService _settings;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly PushServiceClient _pushClient;

        public PushNotificationService(
            AppDbContext db,
            ISettingService settings,
            ILogger<PushNotificationService> logger, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
            _settings = settings;
            _logger = logger;
            _pushClient = new PushServiceClient();
        }

        public async Task SendPushNotificationAsync(int? userId, int? tenantId, string title, string body, string? url)
        {
            try
            {
                var globalPushEnabled = await _settings.GetValueAsync(SettingKeys.NotificationPushEnabled, true);
                if (!globalPushEnabled)
                {
                    _logger.LogInformation("Web Push متوقف من إعدادات النظام.");
                    return;
                }

                var subject = _configuration["VapidDetails:Subject"] ?? "mailto:info@andalos.ly";
                var publicKey = _configuration["VapidDetails:PublicKey"];
                var privateKey = _configuration["VapidDetails:PrivateKey"];

                if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey) || privateKey.Contains("SET_IN_"))
                {
                    _logger.LogWarning("VAPID deployment configuration is missing.");
                    return;
                }

                _pushClient.DefaultAuthentication = new VapidAuthentication(publicKey, privateKey)
                {
                    Subject = subject
                };

                // No tenant-wide fallback: each caller must resolve one live, authorized account first.
                if (!userId.HasValue) throw new ForbiddenOperationException();
                var query = _db.PushSubscriptions.Where(p => p.IsActive);

                if (tenantId.HasValue && userId.HasValue)
                    query = query.Where(p => p.UserId == userId.Value && p.TenantId == tenantId.Value);
                else if (userId.HasValue)
                    query = query.Where(p => p.UserId == userId.Value);
                else
                    return;

                var subscriptions = await query.ToListAsync();
                if (!subscriptions.Any())
                {
                    _logger.LogInformation($"لا توجد أجهزة هاتف مسجلة لـ: TenantId={tenantId}, UserId={userId}");
                    return;
                }

                // 💡 تصحيح حرج: بناء روابط مطلقة كاملة للأيقونات بصيغة PNG حصرياً لـ iOS
                string domain = "https://tenant.marinaalandalus.com"; // رابط الفرونت اند الرئيسي الخاص بك
                string iconUrl = $"{domain}/assets/gold_logo-removebg.png"; // 👈 استخدام الـ PNG بدلاً من SVG

                var payload = JsonSerializer.Serialize(new
                {
                    notification = new
                    {
                        title = title,
                        body = body,
                        icon = iconUrl,          // 👈 رابط كامل PNG
                        badge = iconUrl,         // 👈 رابط كامل PNG
                        vibrate = new[] { 200, 100, 200 },
                        data = new { url = url ?? "/portal/dashboard" }
                    }
                });

                _logger.LogInformation($"جاري إرسال إشعار الهاتف إلى {subscriptions.Count} جهاز...");

                bool transientFailure = false;
                foreach (var sub in subscriptions)
                {
                    if (!PushSubscriptionSecurity.IsAllowedEndpoint(sub.Endpoint)) { sub.IsActive = false; continue; }
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

                        await _pushClient.RequestPushMessageDeliveryAsync(pushSubscription, new PushMessage(payload));
                        sub.LastUsedAt = DateTime.UtcNow;
                    }
                    catch (PushServiceClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        sub.IsActive = false; // إلغاء تفعيل الأجهزة القديمة
                    }
                    catch (Exception ex)
                    {
                        transientFailure = true;
                        _logger.LogWarning("Push delivery failed for subscription {Id}; details redacted.", sub.Id);
                    }
                }

                await _db.SaveChangesAsync();
                if (transientFailure) throw new InvalidOperationException("Push transport unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Push transport unavailable; details redacted.");
                throw new InvalidOperationException("Push transport unavailable.");
            }
        }
    }
}