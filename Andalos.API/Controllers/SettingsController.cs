using Andalos.API.Security;
using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Settings;
using Andalos.API.DTOs.System;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        // GET: api/settings (كل الإعدادات مجمعة)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var groups = await _settingService.GetGroupedSettingsAsync();
            return Ok(ApiResponseDto<List<SettingGroupDto>>.SuccessResponse(groups));
        }

        // GET: api/settings/group/Numbering
        [HttpGet("group/{group}")]
        public async Task<IActionResult> GetByGroup(string group)
        {
            var settings = await _settingService.GetGroupAsync(group);
            return Ok(ApiResponseDto<Dictionary<string, string?>>.SuccessResponse(settings));
        }

        // GET: api/settings/key/Numbering.ContractFormat
        [HttpGet("key/{key}")]
        public async Task<IActionResult> GetByKey(string key)
        {
            if (AuditRedaction.IsSecretSetting(key)) throw new ForbiddenOperationException();
            var value = await _settingService.GetValueAsync(key);
            return Ok(ApiResponseDto<string?>.SuccessResponse(value));
        }

        // PUT: api/settings
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSettingsDto dto)
        {
            if (dto.Values is null || dto.Values.Keys.Any(AuditRedaction.IsSecretSetting)) throw new ForbiddenOperationException();
            string user = User.Identity?.Name ?? "Admin";
            foreach (var kv in dto.Values)
            {
                await _settingService.SetValueAsync(kv.Key, kv.Value, user);
            }
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حفظ الإعدادات بنجاح"));
        }

        // POST: api/settings/reset/Numbering.ContractFormat
        [HttpPost("reset/{key}")]
        public async Task<IActionResult> Reset(string key)
        {
            if (AuditRedaction.IsSecretSetting(key)) throw new ForbiddenOperationException();
            await _settingService.ResetToDefaultAsync(key);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إعادة الإعداد للقيمة الافتراضية"));
        }

        // POST: api/Settings/reset-database (تفريغ كامل لقاعدة البيانات للوضع المصنعي)
        [HttpPost("reset-database")]
        [Authorize(Roles = "SuperAdmin")] // 👈 حماية صارمة
        public async Task<IActionResult> ResetDatabase(
            [FromBody] ResetSystemDto dto,
            [FromServices] ISystemResetService resetService)
        {
            try
            {
                int currentUserId = HttpContext.RequestServices.GetRequiredService<CurrentUser>().UserId;
                var result = await resetService.ResetDatabaseToFactoryDefaultsAsync(dto, currentUserId);

                return Ok(ApiResponseDto<bool>.SuccessResponse(result, "تم تفريغ كافة بيانات المنظومة التشغيلية وإعادتها لوضع المصنع بنجاح."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponseDto<bool>.FailResponse(ex.Message));
            }
            catch (Exception ex) when (ex is not Andalos.API.Security.ForbiddenOperationException and not Andalos.API.Security.ConcurrencyConflictException and not Microsoft.EntityFrameworkCore.DbUpdateException and not System.Data.Common.DbException)
            {
                return BadRequest(ApiResponseDto<bool>.FailResponse("تعذر إعادة ضبط النظام."));
            }
        }

        // GET: api/settings/system-time (فحص واختبار التوقيت الحالي المعتمد بالنظام)
        [HttpGet("system-time")]
        public IActionResult GetSystemTime()
        {
            var libyaNow = DateTimeHelper.LibyaNow;
            var utcNow = DateTime.UtcNow;

            var timeInfo = new
            {
                systemLibyaTime = libyaNow,                            // التوقيت المعتمد في كل العمليات (UTC+2)
                systemLibyaToday = DateTimeHelper.LibyaToday,          // تاريخ اليوم المعتمد
                serverUtcTime = utcNow,                                // توقيت السيرفر العالمي الأصلي
                timeZone = "Africa/Tripoli (UTC+2)",
                formattedDate = libyaNow.ToString("yyyy/MM/dd"),      // التاريخ المنسق
                formattedTime = libyaNow.ToString("hh:mm:ss tt"),      // الوقت المنسق (12 ساعة)
                fullFormatted = libyaNow.ToString("yyyy/MM/dd - hh:mm:ss tt") // التاريخ والوقت كاملاً
            };

            return Ok(ApiResponseDto<object>.SuccessResponse(timeInfo, "التوقيت الحالي المعتمد داخل كافة عمليات المنظومة"));
        }
    }
}
