using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Notifications;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _service;
        private readonly AppDbContext _db;
        private readonly ISettingService _settings;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(
            INotificationService service,
            AppDbContext db,
            ISettingService settings,
            ILogger<NotificationsController> logger)
        {
            _service = service;
            _db = db;
            _settings = settings;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════
        // 1. تسجيل اشتراك جهاز الهاتف (Push Subscription)
        // ═══════════════════════════════════════════════════════════
        [HttpPost("subscribe")]
        public async Task<IActionResult> SubscribeToPush([FromBody] SubscribeToPushDto dto, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);

            if (string.IsNullOrWhiteSpace(dto.Endpoint) || string.IsNullOrWhiteSpace(dto.P256dh) || string.IsNullOrWhiteSpace(dto.Auth))
            {
                return BadRequest(ApiResponseDto<bool>.FailResponse("بيانات اشتراك الجهاز غير مكتملة"));
            }

            var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == dto.Endpoint);

            if (existing == null)
            {
                var sub = new PushSubscription
                {
                    UserId = userId,
                    TenantId = tenantId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth,
                    DeviceInfo = dto.DeviceInfo,
                    BrowserType = dto.BrowserType,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };
                _db.PushSubscriptions.Add(sub);
            }
            else
            {
                existing.UserId = userId;
                existing.TenantId = tenantId;
                existing.P256dh = dto.P256dh;
                existing.Auth = dto.Auth;
                existing.DeviceInfo = dto.DeviceInfo;
                existing.BrowserType = dto.BrowserType;
                existing.IsActive = true;
                existing.LastUsedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation($"✅ تم تسجيل جهاز جديد بنجاح لـ: UserId={userId}, TenantId={tenantId}");

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تفعيل إشعارات الهاتف بنجاح"));
        }

        // ═══════════════════════════════════════════════════════════
        // 2. جلب وتثبيت مفتاح VAPID العام
        // ═══════════════════════════════════════════════════════════
        [HttpGet("vapid-public-key")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVapidPublicKey()
        {
            var publicKey = await _settings.GetValueAsync(SettingKeys.NotificationVapidPublicKey);
            var privateKey = await _settings.GetValueAsync(SettingKeys.NotificationVapidPrivateKey);

            // إذا لم تكن المفاتيح موجودة في قاعدة البيانات، نقوم بتوليدها وتخزينها
            if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
            {
                using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var parameters = ecdsa.ExportExplicitParameters(true);

                var x = parameters.Q.X!;
                var y = parameters.Q.Y!;
                var publicKeyBytes = new byte[1 + x.Length + y.Length];
                publicKeyBytes[0] = 0x04;
                Buffer.BlockCopy(x, 0, publicKeyBytes, 1, x.Length);
                Buffer.BlockCopy(y, 0, publicKeyBytes, 1 + x.Length, y.Length);

                publicKey = WebEncoders.Base64UrlEncode(publicKeyBytes);
                privateKey = WebEncoders.Base64UrlEncode(parameters.D!);

                // 👈 تم تمرير المعامل الثالث "System" بنجاح
                await _settings.SetValueAsync(SettingKeys.NotificationVapidPublicKey, publicKey, "System");
                await _settings.SetValueAsync(SettingKeys.NotificationVapidPrivateKey, privateKey, "System");
                await _settings.SetValueAsync(SettingKeys.NotificationVapidSubject, "mailto:info@andalos.ly", "System");
                await _settings.SetValueAsync(SettingKeys.NotificationPushEnabled, "true", "System");
            }

            return Ok(new
            {
                publicKey = publicKey,
                subject = "mailto:info@andalos.ly"
            });
        }

        // 3. ملخص الإشعارات (للجرس 🔔)
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var summary = await _service.GetMyNotificationsAsync(userId, tenantId);
            return Ok(ApiResponseDto<NotificationSummaryDto>.SuccessResponse(summary));
        }

        // 4. كل الإشعارات
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int limit = 50,
            [FromQuery] int? testUserId = null,
            [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var list = await _service.GetAllAsync(userId, tenantId, unreadOnly, limit);
            return Ok(ApiResponseDto<List<NotificationResponseDto>>.SuccessResponse(list));
        }

        // 5. عدد الإشعارات غير المقروءة
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var count = await _service.GetUnreadCountAsync(userId, tenantId);
            return Ok(ApiResponseDto<int>.SuccessResponse(count));
        }

        // 6. تعليم إشعار كمقروء
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var result = await _service.MarkAsReadAsync(id, userId, tenantId);
            if (!result) return NotFound(ApiResponseDto<bool>.FailResponse("الإشعار غير موجود"));
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعليم الإشعار كمقروء"));
        }

        // 7. تعليم كل الإشعارات كمقروءة
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            await _service.MarkAllAsReadAsync(userId, tenantId);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعليم كل الإشعارات كمقروءة"));
        }

        // 8. حذف إشعار
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var result = await _service.DeleteAsync(id, userId, tenantId);
            if (!result) return NotFound(ApiResponseDto<bool>.FailResponse("الإشعار غير موجود"));
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الإشعار"));
        }

        // 9. تفضيلات الإشعارات
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var prefs = await _service.GetPreferencesAsync(userId, tenantId);
            return Ok(ApiResponseDto<List<NotificationPreferenceDto>>.SuccessResponse(prefs));
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            await _service.UpdatePreferencesAsync(userId, tenantId, dto);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تحديث التفضيلات"));
        }

        // 10. إرسال تجريبي
        [HttpPost("test-send")]
        public async Task<IActionResult> TestSend([FromBody] CreateNotificationDto dto)
        {
            if (!dto.UserId.HasValue && !dto.TenantId.HasValue)
            {
                var (userId, tenantId) = ResolveContext();
                dto.UserId = userId;
                dto.TenantId = tenantId;
            }

            var result = await _service.CreateNotificationAsync(dto);
            return Ok(ApiResponseDto<NotificationResponseDto>.SuccessResponse(result, "تم إرسال الإشعار بنجاح عبر النظام والـ SignalR والـ Push"));
        }

        private (int? userId, int? tenantId) ResolveContext(int? queryUserId = null, int? queryTenantId = null)
        {
            if (queryUserId.HasValue || queryTenantId.HasValue) return (queryUserId, queryTenantId);

            int? headerUserId = null;
            int? headerTenantId = null;

            if (Request.Headers.TryGetValue("X-Test-User-Id", out var hUserId) && int.TryParse(hUserId, out var uid))
                headerUserId = uid;

            if (Request.Headers.TryGetValue("X-Test-Tenant-Id", out var hTenantId) && int.TryParse(hTenantId, out var tid))
                headerTenantId = tid;

            if (headerUserId.HasValue || headerTenantId.HasValue) return (headerUserId, headerTenantId);

            int? claimsUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var cUid) ? cUid : null;
            int? claimsTenantId = int.TryParse(User.FindFirst("TenantId")?.Value, out var cTid) ? cTid : null;

            return (claimsUserId, claimsTenantId);
        }
    }
}