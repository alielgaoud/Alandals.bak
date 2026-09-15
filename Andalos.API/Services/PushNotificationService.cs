using Andalos.API.Constants;
using Andalos.API.Data;
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
        private readonly ISettingService _settings;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly PushServiceClient _pushClient;

        public PushNotificationService(
            AppDbContext db,
            ISettingService settings,
            ILogger<PushNotificationService> logger)
        {
            _db = db;
            _settings = settings;
            _logger = logger;
            _pushClient = new PushServiceClient();
        }

        public async Task SendPushNotificationAsync(int? userId, int? tenantId, string title, string body, string? url)
        {
            try
            {
                // 1) الزر العام من الإعدادات: تفعيل/إيقاف Web Push على مستوى النظام
                var globalPushEnabled = await _settings.GetValueAsync(SettingKeys.NotificationPushEnabled, false);
                if (!globalPushEnabled)
                {
                    _logger.LogDebug("Web Push متوقف من إعدادات النظام.");
                    return;
                }

                // 2) قراءة مفاتيح VAPID من Settings (تتحدث فوراً بعد الحفظ بسبب Cache)
                var subject = await _settings.GetValueAsync(SettingKeys.NotificationVapidSubject)
                              ?? "mailto:info@andalos.ly";
                var publicKey = await _settings.GetValueAsync(SettingKeys.NotificationVapidPublicKey);
                var privateKey = await _settings.GetValueAsync(SettingKeys.NotificationVapidPrivateKey);

                if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
                {
                    _logger.LogWarning("مفاتيح VAPID غير مكتملة في الإعدادات. تم تجاهل Web Push.");
                    return;
                }

                // 3) تهيئة المصادقة ديناميكياً عند كل إرسال
                try
                {
                    _pushClient.DefaultAuthentication = new VapidAuthentication(publicKey, privateKey)
                    {
                        Subject = subject
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "مفاتيح VAPID في الإعدادات غير صالحة.");
                    return;
                }

                // 4) جلب اشتراكات المستلم
                var query = _db.PushSubscriptions.Where(p => p.IsActive);

                if (userId.HasValue)
                    query = query.Where(p => p.UserId == userId);
                else if (tenantId.HasValue)
                    query = query.Where(p => p.TenantId == tenantId);
                else
                    return;

                var subscriptions = await query.ToListAsync();
                if (!subscriptions.Any())
                {
                    _logger.LogDebug("لا توجد اشتراكات Push نشطة للمستلم.");
                    return;
                }

                // 5) بناء Payload القياسي لـ Service Worker
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

                // 6) الإرسال لكل جهاز/متصفح
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

                        await _pushClient.RequestPushMessageDeliveryAsync(pushSubscription, new PushMessage(payload));
                        sub.LastUsedAt = DateTime.UtcNow;
                    }
                    catch (PushServiceClientException ex)
                        when (ex.StatusCode == System.Net.HttpStatusCode.Gone ||
                              ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // الاشتراك منتهٍ أو محذوف من المتصفح
                        sub.IsActive = false;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "فشل إرسال Push للاشتراك: {Endpoint}", sub.Endpoint);
                    }
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // لا نكسر مسار العمل الأساسي إذا فشل Push
                _logger.LogError(ex, "خطأ غير متوقع أثناء إرسال Web Push.");
            }
        }
    }
}