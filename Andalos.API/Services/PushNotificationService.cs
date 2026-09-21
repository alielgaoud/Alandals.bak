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
                var globalPushEnabled = await _settings.GetValueAsync(SettingKeys.NotificationPushEnabled, true);
                if (!globalPushEnabled)
                {
                    _logger.LogInformation("Web Push متوقف من إعدادات النظام.");
                    return;
                }

                var subject = await _settings.GetValueAsync(SettingKeys.NotificationVapidSubject) ?? "mailto:info@andalos.ly";
                var publicKey = await _settings.GetValueAsync(SettingKeys.NotificationVapidPublicKey);
                var privateKey = await _settings.GetValueAsync(SettingKeys.NotificationVapidPrivateKey);

                if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
                {
                    _logger.LogWarning("مفاتيح VAPID غير موجودة في قاعدة البيانات.");
                    return;
                }

                _pushClient.DefaultAuthentication = new VapidAuthentication(publicKey, privateKey)
                {
                    Subject = subject
                };

                var query = _db.PushSubscriptions.Where(p => p.IsActive);

                if (tenantId.HasValue && userId.HasValue)
                    query = query.Where(p => p.TenantId == tenantId.Value || p.UserId == userId.Value);
                else if (tenantId.HasValue)
                    query = query.Where(p => p.TenantId == tenantId.Value);
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

                        var pushMessage = new PushMessage(payload)
                        {
                            Urgency = PushMessageUrgency.High,
                            TimeToLive = 86400
                        };

                        await _pushClient.RequestPushMessageDeliveryAsync(pushSubscription, pushMessage);
                        sub.LastUsedAt = DateTime.UtcNow;
                        _logger.LogInformation($"✅ تم تسليم الإشعار بنجاح للجهاز: {sub.DeviceInfo}");
                    }
                    catch (PushServiceClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        sub.IsActive = false; // إلغاء تفعيل الأجهزة القديمة
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
                _logger.LogError(ex, "خطأ غير متوقع أثناء إرسال Web Push.");
            }
        }
    }
}