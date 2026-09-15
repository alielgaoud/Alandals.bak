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

        public NotificationsController(INotificationService service)
        {
            _service = service;
        }

        // 1. ملخص إشعاراتي (للجرس 🔔)
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var summary = await _service.GetMyNotificationsAsync(userId, tenantId);
            return Ok(ApiResponseDto<NotificationSummaryDto>.SuccessResponse(summary));
        }

        // 2. كل الإشعارات (للصفحة الكاملة)
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool unreadOnly = false, [FromQuery] int limit = 50)
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var list = await _service.GetAllAsync(userId, tenantId, unreadOnly, limit);
            return Ok(ApiResponseDto<List<NotificationResponseDto>>.SuccessResponse(list));
        }

        // 3. عدد الإشعارات غير المقروءة
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var count = await _service.GetUnreadCountAsync(userId, tenantId);
            return Ok(ApiResponseDto<int>.SuccessResponse(count));
        }

        // 4. تعليم إشعار كمقروء
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var result = await _service.MarkAsReadAsync(id, userId, tenantId);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الإشعار غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعليم الإشعار كمقروء"));
        }

        // 5. تعليم كل الإشعارات كمقروءة
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var result = await _service.MarkAllAsReadAsync(userId, tenantId);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعليم كل الإشعارات كمقروءة"));
        }

        // 6. حذف إشعار
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var result = await _service.DeleteAsync(id, userId, tenantId);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الإشعار غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الإشعار"));
        }

        // 7. جلب التفضيلات
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var prefs = await _service.GetPreferencesAsync(userId, tenantId);
            return Ok(ApiResponseDto<List<NotificationPreferenceDto>>.SuccessResponse(prefs));
        }

        // 8. تحديث التفضيلات
        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesDto dto)
        {
            var (userId, tenantId) = GetCurrentUserContext();
            var result = await _service.UpdatePreferencesAsync(userId, tenantId, dto);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تحديث تفضيلات الإشعارات بنجاح"));
        }
        
        // 👈 دالة تجريبية لإرسال إشعار فوراً واختباره
        [HttpPost("test-send")]
        public async Task<IActionResult> TestSend([FromBody] CreateNotificationDto dto)
        {
            var (userId, tenantId) = GetCurrentUserContext();

            // إسناد المستخدم الحالي إذا لم يحدد مستقبل
            if (!dto.UserId.HasValue && !dto.TenantId.HasValue)
            {
                dto.UserId = userId;
            }

            var result = await _service.CreateNotificationAsync(dto);
            return Ok(ApiResponseDto<NotificationResponseDto>.SuccessResponse(result, "تم إرسال الإشعار بنجاح عبر النظام والـ SignalR"));
        }

        // 👈 دالة توليد مفاتيح VAPID مجانية ومطابقة للمواصفات
        [HttpGet("generate-vapid-keys")]
        [AllowAnonymous]
        public IActionResult GenerateVapidKeys()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = ecdsa.ExportExplicitParameters(true);

            // Q.X and Q.Y for Public Key (Uncompressed format: 0x04 + X + Y)
            var x = parameters.Q.X!;
            var y = parameters.Q.Y!;
            var publicKeyBytes = new byte[1 + x.Length + y.Length];
            publicKeyBytes[0] = 0x04;
            Buffer.BlockCopy(x, 0, publicKeyBytes, 1, x.Length);
            Buffer.BlockCopy(y, 0, publicKeyBytes, 1 + x.Length, y.Length);

            // D for Private Key (32 bytes)
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
        // دوال مساعدة
        // =====================================================
        private (int? userId, int? tenantId) GetCurrentUserContext()
        {
            int? userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
            int? tenantId = int.TryParse(User.FindFirst("TenantId")?.Value, out var tid) ? tid : null;
            return (userId, tenantId);
        }
    }
}