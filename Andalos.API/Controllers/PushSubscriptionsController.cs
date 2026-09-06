using Andalos.API.Data;
using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PushSubscriptionsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public PushSubscriptionsController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        // 1. جلب المفتاح العام للـ Angular
        [HttpGet("public-key")]
        [AllowAnonymous]
        public IActionResult GetPublicKey()
        {
            return Ok(new { publicKey = _config["VapidDetails:PublicKey"] });
        }

        // 2. تسجيل متصفح جديد (اشتراك)
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeToPushDto dto)
        {
            int? userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
            int? tenantId = int.TryParse(User.FindFirst("TenantId")?.Value, out var tid) ? tid : null;

            var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == dto.Endpoint);

            if (existing == null)
            {
                _db.PushSubscriptions.Add(new Models.PushSubscription
                {
                    UserId = userId,
                    TenantId = tenantId,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth,
                    DeviceInfo = dto.DeviceInfo,
                    BrowserType = dto.BrowserType,
                    LastUsedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.UserId = userId;
                existing.TenantId = tenantId;
                existing.P256dh = dto.P256dh;
                existing.Auth = dto.Auth;
                existing.LastUsedAt = DateTime.UtcNow;
                existing.IsActive = true;
            }

            await _db.SaveChangesAsync();
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تفعيل الإشعارات بنجاح"));
        }

        // 3. إلغاء الاشتراك
        [HttpPost("unsubscribe")]
        public async Task<IActionResult> Unsubscribe([FromBody] string endpoint)
        {
            var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(p => p.Endpoint == endpoint);
            if (existing != null)
            {
                _db.PushSubscriptions.Remove(existing);
                await _db.SaveChangesAsync();
            }
            return Ok(ApiResponseDto<bool>.SuccessResponse(true));
        }
    }
}