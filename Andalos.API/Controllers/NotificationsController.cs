using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Notifications;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(INotificationService service, ILogger<NotificationsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // 1. ملخص إشعاراتي (للجرس 🔔)
        // 💡 تم التطوير: يمكنك الآن تمرير ?testTenantId=1 أو ?testUserId=2 لتجربتها مباشرة من Swagger!
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            _logger.LogInformation($"Fetching Summary for: UserId={userId}, TenantId={tenantId}");

            var summary = await _service.GetMyNotificationsAsync(userId, tenantId);
            return Ok(ApiResponseDto<NotificationSummaryDto>.SuccessResponse(summary));
        }

        // 2. كل الإشعارات (للصفحة الكاملة)
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int limit = 50,
            [FromQuery] int? testUserId = null,
            [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            _logger.LogInformation($"Fetching All Notifications for: UserId={userId}, TenantId={tenantId}");

            var list = await _service.GetAllAsync(userId, tenantId, unreadOnly, limit);
            return Ok(ApiResponseDto<List<NotificationResponseDto>>.SuccessResponse(list));
        }

        // 3. عدد الإشعارات غير المقروءة
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var count = await _service.GetUnreadCountAsync(userId, tenantId);
            return Ok(ApiResponseDto<int>.SuccessResponse(count));
        }

        // 4. تعليم إشعار كمقروء
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var result = await _service.MarkAsReadAsync(id, userId, tenantId);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الإشعار غير موجود أو لا تملك صلاحية الوصول إليه"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعليم الإشعار كمقروء"));
        }

        // 5. تعليم كل الإشعارات كمقروءة
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var result = await _service.MarkAllAsReadAsync(userId, tenantId);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعليم كل الإشعارات كمقروءة"));
        }

        // 6. حذف إشعار
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var result = await _service.DeleteAsync(id, userId, tenantId);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الإشعار غير موجود أو لا تملك صلاحية الوصول إليه"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الإشعار بنجاح"));
        }

        // 7. جلب تفضيلات الإشعارات
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences([FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var prefs = await _service.GetPreferencesAsync(userId, tenantId);
            return Ok(ApiResponseDto<List<NotificationPreferenceDto>>.SuccessResponse(prefs));
        }

        // 8. تحديث التفضيلات
        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto, [FromQuery] int? testUserId = null, [FromQuery] int? testTenantId = null)
        {
            var (userId, tenantId) = ResolveContext(testUserId, testTenantId);
            var result = await _service.UpdatePreferencesAsync(userId, tenantId, dto);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تحديث تفضيلات الإشعارات بنجاح"));
        }

        // 9. 👈 دالة تجريبية مطورة لإرسال إشعار واختباره فوراً مع إمكانية تحديد المستهدف بدقة
        [HttpPost("test-send")]
        public async Task<IActionResult> TestSend([FromBody] CreateNotificationDto dto)
        {
            // إذا لم يحدد المستهدف في الـ Body، نقرأ سياق الطلب الحالي كمسار بديل للإنتاج
            if (!dto.UserId.HasValue && !dto.TenantId.HasValue)
            {
                var (userId, tenantId) = ResolveContext();
                dto.UserId = userId;
                dto.TenantId = tenantId;
            }

            _logger.LogInformation($"Executing TestSend Target: UserId={dto.UserId}, TenantId={dto.TenantId}");

            var result = await _service.CreateNotificationAsync(dto);
            return Ok(ApiResponseDto<NotificationResponseDto>.SuccessResponse(result, "تم إرسال الإشعار بنجاح عبر النظام والـ SignalR"));
        }

        // 10. توليد مفاتيح VAPID
        [HttpGet("generate-vapid-keys")]
        [AllowAnonymous]
        public IActionResult GenerateVapidKeys()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = ecdsa.ExportExplicitParameters(true);

            var x = parameters.Q.X!;
            var y = parameters.Q.Y!;
            var publicKeyBytes = new byte[1 + x.Length + y.Length];
            publicKeyBytes[0] = 0x04;
            Buffer.BlockCopy(x, 0, publicKeyBytes, 1, x.Length);
            Buffer.BlockCopy(y, 0, publicKeyBytes, 1 + x.Length, y.Length);

            var privateKeyBytes = parameters.D!;

            var publicKey = WebEncoders.Base64UrlEncode(publicKeyBytes);
            var privateKey = WebEncoders.Base64UrlEncode(privateKeyBytes);

            return Ok(new
            {
                subject = "mailto:info@andalos.ly",
                publicKey = publicKey,
                privateKey = privateKey
            });
        }

        // =====================================================
        // 🔄 دالة تفكيك واستخلاص الهوية المركبة والمحسنة 100%
        // =====================================================
        private (int? userId, int? tenantId) ResolveContext(int? queryUserId = null, int? queryTenantId = null)
        {
            // 1. الأولوية الأولى: المعاملات الممررة في الرابط (Swagger/Testing Override)
            if (queryUserId.HasValue || queryTenantId.HasValue)
            {
                return (queryUserId, queryTenantId);
            }

            // 2. الأولوية الثانية: هيدرز التطوير الممررة من الـ Interceptor الخاص بالأنغولار
            int? headerUserId = null;
            int? headerTenantId = null;

            if (Request.Headers.TryGetValue("X-Test-User-Id", out var hUserId) && int.TryParse(hUserId, out var uid))
                headerUserId = uid;

            if (Request.Headers.TryGetValue("X-Test-Tenant-Id", out var hTenantId) && int.TryParse(hTenantId, out var tid))
                headerTenantId = tid;

            if (headerUserId.HasValue || headerTenantId.HasValue)
            {
                return (headerUserId, headerTenantId);
            }

            // 3. الأولوية الثالثة: التوكن القياسي المستخلص من الصلاحيات (Production Fallback)
            int? claimsUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var cUid) ? cUid : null;
            int? claimsTenantId = int.TryParse(User.FindFirst("TenantId")?.Value, out var cTid) ? cTid : null;

            return (claimsUserId, claimsTenantId);
        }
    }
}