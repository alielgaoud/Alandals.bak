using Andalos.API.Constants;
using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Settings;
using Andalos.API.DTOs.System;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        // GET: api/settings/preview/Expense
        [HttpGet("preview/{sequenceKey}")]
        public async Task<IActionResult> PreviewNumber(string sequenceKey, [FromServices] INumberGeneratorService numberGen)
        {
            try
            {
                string seqKey = sequenceKey.Trim();
                string formatKey = $"Numbering.{seqKey}Format";
                string prefixKey = $"Numbering.{seqKey}Prefix";

                // دعم الأسماء المختلفة
                var keyMap = new Dictionary<string, (string format, string prefix)>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Contract", (SettingKeys.ContractNumberFormat, SettingKeys.ContractNumberPrefix) },
                    { "Receipt", (SettingKeys.ReceiptNumberFormat, SettingKeys.ReceiptNumberPrefix) },
                    { "Payment", (SettingKeys.ReceiptNumberFormat, SettingKeys.ReceiptNumberPrefix) },
                    { "Maintenance", (SettingKeys.MaintenanceNumberFormat, SettingKeys.MaintenanceNumberPrefix) },
                    { "Expense", (SettingKeys.ExpenseNumberFormat, SettingKeys.ExpenseNumberPrefix) },
                    { "PassCode", (SettingKeys.PassCodeFormat, SettingKeys.PassCodePrefix) },
                    { "Pass", (SettingKeys.PassCodeFormat, SettingKeys.PassCodePrefix) },
                    { "Refund", (SettingKeys.RefundNumberFormat, SettingKeys.RefundNumberPrefix) },
                };

                if (keyMap.TryGetValue(seqKey, out var mapped))
                {
                    formatKey = mapped.format;
                    prefixKey = mapped.prefix;
                }

                var format = await _settingService.GetValueAsync(formatKey) ?? "(غير موجود)";
                var prefix = await _settingService.GetValueAsync(prefixKey) ?? "(غير موجود)";
                var preview = await numberGen.PreviewNextNumberAsync(seqKey, formatKey, prefixKey);

                var result = new
                {
                    sequenceKey = seqKey,
                    formatKey,
                    formatValue = format,
                    prefixKey,
                    prefixValue = prefix,
                    nextNumberPreview = preview,
                    note = "هذه معاينة للرقم القادم حسب الإعدادات الحالية - لن يزيد العداد"
                };

                return Ok(ApiResponseDto<object>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<object>.FailResponse($"خطأ في المعاينة: {ex.Message}"));
            }
        }

        // GET: api/settings/numbering/overview - نظرة شاملة على كل الترقيمات
        [HttpGet("numbering/overview")]
        public async Task<IActionResult> GetNumberingOverview([FromServices] INumberGeneratorService numberGen, [FromServices] Andalos.API.Data.AppDbContext db)
        {
            var sequences = new[]
            {
                new { Key = "Contract", FormatKey = SettingKeys.ContractNumberFormat, PrefixKey = SettingKeys.ContractNumberPrefix, Model = "Contracts.ContractNumber", Service = "ContractService" },
                new { Key = "Receipt", FormatKey = SettingKeys.ReceiptNumberFormat, PrefixKey = SettingKeys.ReceiptNumberPrefix, Model = "Payments.ReceiptNumber", Service = "PaymentService, TenantAccountService, ContractService.ProcessDue, ExpenseService" },
                new { Key = "Maintenance", FormatKey = SettingKeys.MaintenanceNumberFormat, PrefixKey = SettingKeys.MaintenanceNumberPrefix, Model = "MaintenanceRequests.RequestNumber", Service = "MaintenanceService" },
                new { Key = "Expense", FormatKey = SettingKeys.ExpenseNumberFormat, PrefixKey = SettingKeys.ExpenseNumberPrefix, Model = "Expenses.ExpenseNumber", Service = "ExpenseService" },
                new { Key = "PassCode", FormatKey = SettingKeys.PassCodeFormat, PrefixKey = SettingKeys.PassCodePrefix, Model = "VisitorPasses.PassCode", Service = "VisitorPassService, VisitorWalletService" },
                new { Key = "Refund", FormatKey = SettingKeys.RefundNumberFormat, PrefixKey = SettingKeys.RefundNumberPrefix, Model = "Refunds.RefundNumber", Service = "RefundService" },
            };

            var result = new List<object>();
            foreach (var seq in sequences)
            {
                var format = await _settingService.GetValueAsync(seq.FormatKey) ?? "(غير موجود)";
                var prefix = await _settingService.GetValueAsync(seq.PrefixKey) ?? "(غير موجود)";
                string preview;
                try { preview = await numberGen.PreviewNextNumberAsync(seq.Key, seq.FormatKey, seq.PrefixKey); }
                catch (Exception ex) { preview = $"خطأ: {ex.Message}"; }

                var dbSeq = await db.NumberSequences.AsNoTracking().FirstOrDefaultAsync(s => s.SequenceKey == seq.Key);
                result.Add(new
                {
                    sequenceKey = seq.Key,
                    formatKey = seq.FormatKey,
                    formatValue = format,
                    prefixKey = seq.PrefixKey,
                    prefixValue = prefix,
                    nextPreview = preview,
                    currentState = dbSeq == null ? null : new { lastNumber = dbSeq.LastNumber, currentYear = dbSeq.CurrentYear, lastYear = dbSeq.LastYear, updatedAt = dbSeq.UpdatedAt },
                    usedInModel = seq.Model,
                    usedInServices = seq.Service,
                    tokens = new[] { "{PREFIX}", "{YYYY}", "{YY}", "{MM}", "{DD}", "{SEQ:n}", "{DATE}", "{DATE:format}", "{RND:n}", "{HEX:n}" },
                    example = $"{prefix}-2025-00001"
                });
            }

            return Ok(ApiResponseDto<object>.SuccessResponse(result, "نظرة شاملة على كل مفاتيح التسلسل وربطها بالإعدادات"));
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

        // GET: api/settings/system-time - يستخدم إعدادات النظام المترابطة
        [HttpGet("system-time")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSystemTime()
        {
            var libyaNow = DateTimeHelper.LibyaNow;
            var utcNow = DateTime.UtcNow;

            // قراءة إعدادات النظام - مترابطة
            var timeZone = await _settingService.GetValueAsync(SettingKeys.SystemTimeZone, "Africa/Tripoli");
            var dateFormat = await _settingService.GetValueAsync(SettingKeys.SystemDateFormat, "DD/MM/YYYY");
            var language = await _settingService.GetValueAsync(SettingKeys.SystemLanguage, "ar");
            var sessionTimeout = await _settingService.GetValueAsync<int>(SettingKeys.SystemSessionTimeout, 30);
            var maintenanceMode = await _settingService.GetValueAsync<bool>(SettingKeys.SystemMaintenanceMode, false);
            var backendUrl = await _settingService.GetValueAsync(SettingKeys.SystemBackendUrl, "https://api.marinaalandalus.com");
            var frontendAdminUrl = await _settingService.GetValueAsync(SettingKeys.SystemFrontendAdminUrl, "https://admin.marinaalandalus.com");
            var frontendTenantUrl = await _settingService.GetValueAsync(SettingKeys.SystemFrontendTenantUrl, "https://tenant.marinaalandalus.com");

            string formattedDate = dateFormat.ToUpper() switch
            {
                "DD/MM/YYYY" => libyaNow.ToString("dd/MM/yyyy"),
                "MM/DD/YYYY" => libyaNow.ToString("MM/dd/yyyy"),
                "YYYY-MM-DD" => libyaNow.ToString("yyyy-MM-dd"),
                "DD-MM-YYYY" => libyaNow.ToString("dd-MM-yyyy"),
                _ => libyaNow.ToString("yyyy/MM/dd")
            };

            var timeInfo = new
            {
                systemLibyaTime = libyaNow,
                systemLibyaToday = DateTimeHelper.LibyaToday,
                serverUtcTime = utcNow,
                timeZone = timeZone,
                dateFormat = dateFormat,
                language = language,
                sessionTimeoutMinutes = sessionTimeout,
                maintenanceMode = maintenanceMode,
                backendUrl = backendUrl,
                frontendAdminUrl = frontendAdminUrl,
                frontendTenantUrl = frontendTenantUrl,
                formattedDate = formattedDate,
                formattedTime = libyaNow.ToString("hh:mm:ss tt"),
                fullFormatted = $"{formattedDate} - {libyaNow:hh:mm:ss tt}",
                note = "كل هذه القيم تأتي من إعدادات النظام المترابطة - تغييرها في الإعدادات يغير سلوك النظام فوراً"
            };

            return Ok(ApiResponseDto<object>.SuccessResponse(timeInfo, "التوقيت الحالي والإعدادات المترابطة للنظام"));
        }
    }
}