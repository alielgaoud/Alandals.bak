using Andalos.API.Constants;
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
        private readonly IWebHostEnvironment _env;

        public SettingsController(ISettingService settingService, IWebHostEnvironment env)
        {
            _settingService = settingService;
            _env = env;
        }

        // GET: api/settings (كل الإعدادات مجمعة)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var groups = await _settingService.GetGroupedSettingsAsync();
            return Ok(ApiResponseDto<List<SettingGroupDto>>.SuccessResponse(groups));
        }

        // GET: api/settings/all-flat (كل الإعدادات كـ قاموس مسطح)
        [HttpGet("all-flat")]
        public async Task<IActionResult> GetAllFlat()
        {
            var dict = await _settingService.GetAllSettingsDictionaryAsync();
            return Ok(ApiResponseDto<Dictionary<string, string?>>.SuccessResponse(dict));
        }

        // GET: api/settings/company-info
        [HttpGet("company-info")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCompanyInfo()
        {
            var info = await _settingService.GetCompanyInfoAsync();
            return Ok(ApiResponseDto<CompanyInfoDto>.SuccessResponse(info));
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
            var value = await _settingService.GetValueAsync(key);
            if (value == null && !await _settingService.SettingExistsAsync(key))
                return NotFound(ApiResponseDto<string?>.FailResponse("الإعداد غير موجود"));

            return Ok(ApiResponseDto<string?>.SuccessResponse(value));
        }

        // PUT: api/settings
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSettingsDto dto)
        {
            string user = User.Identity?.Name ?? "Admin";
            foreach (var kv in dto.Values)
            {
                await _settingService.SetValueAsync(kv.Key, kv.Value, user);
            }
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حفظ الإعدادات بنجاح"));
        }

        // POST: api/settings/create - إنشاء إعداد جديد مترابط
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateSettingDto dto)
        {
            try
            {
                string user = User.Identity?.Name ?? "Admin";
                var created = await _settingService.CreateSettingAsync(dto, user);
                return Ok(ApiResponseDto<SettingResponseDto>.SuccessResponse(created, "تم إنشاء الإعداد الجديد وربطه بالنظام بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<SettingResponseDto>.FailResponse(ex.Message));
            }
        }

        // DELETE: api/settings/key/{key}
        [HttpDelete("key/{key}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(string key)
        {
            try
            {
                var result = await _settingService.DeleteSettingAsync(key);
                if (!result)
                    return NotFound(ApiResponseDto<bool>.FailResponse("الإعداد غير موجود"));

                return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الإعداد بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<bool>.FailResponse(ex.Message));
            }
        }

        // POST: api/settings/logo - رفع شعار الشركة
        [HttpPost("logo")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadLogo([FromForm] IFormFile file, [FromForm] string type = "logo")
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(ApiResponseDto<string>.FailResponse("الملف مطلوب"));

                var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".svg", ".webp" };
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                    return BadRequest(ApiResponseDto<string>.FailResponse("نوع الملف غير مدعوم، استخدم PNG, JPG, SVG, WEBP"));

                string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                string logosFolder = Path.Combine(webRoot, "uploads", "logos");
                if (!Directory.Exists(logosFolder))
                    Directory.CreateDirectory(logosFolder);

                string fileName = type.ToLower() switch
                {
                    "favicon" => $"favicon{ext}",
                    "stamp" => $"stamp{ext}",
                    _ => $"logo{ext}"
                };

                // حذف القديم إذا موجود
                foreach (var oldFile in Directory.GetFiles(logosFolder, $"{Path.GetFileNameWithoutExtension(fileName)}.*"))
                {
                    try { System.IO.File.Delete(oldFile); } catch { }
                }

                string fullPath = Path.Combine(logosFolder, fileName);
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                string relativePath = $"/uploads/logos/{fileName}";
                string user = User.Identity?.Name ?? "Admin";

                string settingKey = type.ToLower() switch
                {
                    "favicon" => SettingKeys.CompanyFaviconUrl,
                    "stamp" => SettingKeys.CompanyStampUrl,
                    _ => SettingKeys.CompanyLogoPath
                };

                await _settingService.SetValueAsync(settingKey, relativePath, user);

                // إذا كان logo، أيضاً نحدث LogoUrl إذا كان فارغاً ليكون نفس المسار النسبي
                if (type.ToLower() == "logo")
                {
                    var frontendTenantUrl = await _settingService.GetValueAsync(SettingKeys.SystemFrontendTenantUrl, "https://tenant.marinaalandalus.com");
                    var fullUrl = $"{frontendTenantUrl.TrimEnd('/')}{relativePath}";
                    // لا نحدث LogoUrl تلقائياً إذا كان موجوداً، فقط إذا كان فارغاً
                    var existingLogoUrl = await _settingService.GetValueAsync(SettingKeys.CompanyLogoUrl);
                    if (string.IsNullOrWhiteSpace(existingLogoUrl))
                    {
                        await _settingService.SetValueAsync(SettingKeys.CompanyLogoUrl, fullUrl, user);
                        await _settingService.SetValueAsync(SettingKeys.NotificationIconUrl, fullUrl, user);
                        await _settingService.SetValueAsync(SettingKeys.NotificationBadgeUrl, fullUrl, user);
                    }
                }

                return Ok(ApiResponseDto<string>.SuccessResponse(relativePath, $"تم رفع {(type == "favicon" ? "الأيقونة" : type == "stamp" ? "الختم" : "الشعار")} بنجاح وتم ربطه بكل ملفات النظام"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse($"خطأ أثناء رفع الملف: {ex.Message}"));
            }
        }

        // POST: api/settings/reset/Numbering.ContractFormat
        [HttpPost("reset/{key}")]
        public async Task<IActionResult> Reset(string key)
        {
            await _settingService.ResetToDefaultAsync(key);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إعادة الإعداد للقيمة الافتراضية"));
        }

        // POST: api/Settings/reset-database
        [HttpPost("reset-database")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> ResetDatabase(
            [FromBody] ResetSystemDto dto,
            [FromServices] ISystemResetService resetService)
        {
            try
            {
                int currentUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
                var result = await resetService.ResetDatabaseToFactoryDefaultsAsync(dto, currentUserId);

                return Ok(ApiResponseDto<bool>.SuccessResponse(result, "تم تفريغ كافة بيانات المنظومة التشغيلية وإعادتها لوضع المصنع بنجاح."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponseDto<bool>.FailResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<bool>.FailResponse($"حدث خطأ أثناء إعادة ضبط النظام: {ex.Message}"));
            }
        }

        // GET: api/settings/system-time
        [HttpGet("system-time")]
        [AllowAnonymous]
        public IActionResult GetSystemTime()
        {
            var libyaNow = DateTimeHelper.LibyaNow;
            var utcNow = DateTime.UtcNow;

            var timeInfo = new
            {
                systemLibyaTime = libyaNow,
                systemLibyaToday = DateTimeHelper.LibyaToday,
                serverUtcTime = utcNow,
                timeZone = "Africa/Tripoli (UTC+2)",
                formattedDate = libyaNow.ToString("yyyy/MM/dd"),
                formattedTime = libyaNow.ToString("hh:mm:ss tt"),
                fullFormatted = libyaNow.ToString("yyyy/MM/dd - hh:mm:ss tt")
            };

            return Ok(ApiResponseDto<object>.SuccessResponse(timeInfo, "التوقيت الحالي المعتمد داخل كافة عمليات المنظومة"));
        }
    }
}