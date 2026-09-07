using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.DTOs.Payments;
using Andalos.API.DTOs.Portal;
using Andalos.API.DTOs.Tenants;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Andalos.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

using TenantAccountStatementDto = Andalos.API.DTOs.Portal.TenantAccountStatementDto;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantPortalController : ControllerBase
    {
        private readonly ITenantPortalService _portalService;
        private readonly IWebHostEnvironment _env;               // 👈 تم إضافة البيئة
        private readonly IBankTransferService _bankTransferService; // 👈 تم إضافة خدمة الحوالات

        public TenantPortalController(
            ITenantPortalService portalService,
            IWebHostEnvironment env,
            IBankTransferService bankTransferService)
        {
            _portalService = portalService;
            _env = env;
            _bankTransferService = bankTransferService;
        }

        // 🔒 دالة مساعدة لحماية الطلبات من هجمات التلاعب بـ IDs (IDOR)
        private bool ValidateCurrentUserTenant(int tenantId)
        {
            // استخراج الدور من الـ Token
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            // إذا كان المستخدم آدمن أو سوبر آدمن فله الحق في العرض دون قيود
            if (role == "SuperAdmin" || role == "Admin") return true;

            // استخراج الـ TenantId المشفر بالـ Token للمستأجر
            var tokenTenantIdClaim = User.FindFirst("TenantId")?.Value;
            if (string.IsNullOrEmpty(tokenTenantIdClaim)) return false;

            return int.TryParse(tokenTenantIdClaim, out int tokenTenantId) && tokenTenantId == tenantId;
        }

        // 1. كشف حساب المستأجر الشامل
        [HttpGet("statement/{tenantId}")]
        public async Task<IActionResult> GetStatement(int tenantId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<TenantAccountStatementDto>.FailResponse("غير مصرح لك بالوصول لبيانات هذا الحساب"));

            try
            {
                var data = await _portalService.GetMyStatementAsync(tenantId);
                return Ok(ApiResponseDto<TenantAccountStatementDto>.SuccessResponse(data));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<TenantAccountStatementDto>.FailResponse(ex.Message));
            }
        }

        // 2. عقود المستأجر
        [HttpGet("contracts/{tenantId}")]
        public async Task<IActionResult> GetContracts(int tenantId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<List<ContractResponseDto>>.FailResponse("غير مصرح لك بالوصول لهذه البيانات"));

            var data = await _portalService.GetMyContractsAsync(tenantId);
            return Ok(ApiResponseDto<List<ContractResponseDto>>.SuccessResponse(data));
        }

        // 3. سجل مدفوعات المستأجر
        [HttpGet("payments/{tenantId}")]
        public async Task<IActionResult> GetPayments(int tenantId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<List<PaymentResponseDto>>.FailResponse("غير مصرح لك بالوصول لهذه البيانات"));

            var data = await _portalService.GetMyPaymentsAsync(tenantId);
            return Ok(ApiResponseDto<List<PaymentResponseDto>>.SuccessResponse(data));
        }

        // 4. رفع طلب صيانة لمحل المستأجر
        [HttpPost("maintenance/{tenantId}")]
        public async Task<IActionResult> RequestMaintenance(int tenantId, [FromBody] TenantCreateMaintenanceDto dto)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<MaintenanceResponseDto>.FailResponse("غير مصرح لك برفع طلب صيانة لهذا الحساب"));

            try
            {
                var result = await _portalService.RequestMaintenanceAsync(tenantId, dto);
                return Ok(ApiResponseDto<MaintenanceResponseDto>.SuccessResponse(result, "تم إرسال طلب الصيانة للإدارة بنجاح"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponseDto<MaintenanceResponseDto>.FailResponse(ex.Message));
            }
        }

        // 5. إنشاء باركود زائر خاص بمحل المستأجر
        [HttpPost("visitor-pass/{tenantId}")]
        public async Task<IActionResult> CreateVisitorPass(int tenantId, [FromBody] TenantCreatePassDto dto)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<VisitorPassResponseDto>.FailResponse("غير مصرح لك بإنشاء تصريح زائر لهذا المحل"));

            try
            {
                string user = User.Identity?.Name ?? "Tenant";
                var result = await _portalService.CreateVisitorPassAsync(tenantId, dto, user);
                return Ok(ApiResponseDto<VisitorPassResponseDto>.SuccessResponse(result, "تم إصدار تصريح الدخول بالباركود بنجاح"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ApiResponseDto<VisitorPassResponseDto>.FailResponse(ex.Message));
            }
        }

        // 6. عرض تصاريح الزوار السابقة للمستأجر
        [HttpGet("visitor-passes/{tenantId}")]
        public async Task<IActionResult> GetVisitorPasses(int tenantId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<List<VisitorPassResponseDto>>.FailResponse("غير مصرح لك بعرض هذه البيانات"));

            var list = await _portalService.GetMyVisitorPassesAsync(tenantId);
            return Ok(ApiResponseDto<List<VisitorPassResponseDto>>.SuccessResponse(list));
        }

        // 7. إنشاء حساب دخول لمستأجر (للإدارة)
        [HttpPost("create-account")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> CreateAccount([FromBody] CreateTenantUserAccountDto dto)
        {
            try
            {
                await _portalService.CreateTenantUserAccountAsync(dto);
                return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إنشاء حساب دخول المستأجر للمنظومة بنجاح"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<bool>.FailResponse(ex.Message));
            }
        }
        [HttpGet("maintenance/{tenantId}")]
        public async Task<IActionResult> GetMyMaintenance(int tenantId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<List<MaintenanceResponseDto>>.FailResponse("غير مصرح"));

            var data = await _portalService.GetMyMaintenanceAsync(tenantId);
            return Ok(ApiResponseDto<List<MaintenanceResponseDto>>.SuccessResponse(data));
        }

        // جلب طلب واحد
        [HttpGet("maintenance/{tenantId}/{requestId}")]
        public async Task<IActionResult> GetMaintenanceById(int tenantId, int requestId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<MaintenanceResponseDto>.FailResponse("غير مصرح"));

            try
            {
                var data = await _portalService.GetMaintenanceByIdAsync(tenantId, requestId);
                return Ok(ApiResponseDto<MaintenanceResponseDto>.SuccessResponse(data));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<MaintenanceResponseDto>.FailResponse(ex.Message));
            }
        }
        [HttpPost("{tenantId}/upload-receipt")]
        [Consumes("multipart/form-data")]
        [DisableRequestSizeLimit] // 👈 لمنع Kestrel من قطع الاتصال أثناء رفع الملف
        public async Task<IActionResult> UploadReceipt(int tenantId, [FromForm] SubmitTransferRequestDto dto)
        {
            try
            {
                if (dto.ReceiptFile == null || dto.ReceiptFile.Length == 0)
                    return BadRequest(ApiResponseDto<string>.FailResponse("صورة واصل الدفع مطلوبة"));

                // 👈 معالجة ذكية: إذا كان WebRootPath نل، نستخدم مجلد wwwroot الفعلي في الجذر
                string webRootPath = _env.WebRootPath;
                if (string.IsNullOrEmpty(webRootPath))
                {
                    webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                }

                // إنشاء مجلد wwwroot إن لم يكن موجوداً
                if (!Directory.Exists(webRootPath))
                {
                    Directory.CreateDirectory(webRootPath);
                }

                var result = await _bankTransferService.SubmitRequestAsync(tenantId, dto, webRootPath);
                return Ok(ApiResponseDto<TransferRequestResponseDto>.SuccessResponse(result, "تم رفع الواصل بنجاح، بانتظار مراجعة الإدارة."));
            }
            catch (Exception ex)
            {
                // 👈 إرجاع JSON واضح في حال حدوث أي خطأ بدلاً من قطع الاتصال
                return BadRequest(ApiResponseDto<string>.FailResponse($"خطأ أثناء رفع الملف: {ex.Message}"));
            }
        }

        [HttpPost("staff/{tenantId}")]
        public async Task<IActionResult> CreateStaff(int tenantId, [FromBody] CreateTenantStaffDto dto)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<bool>.FailResponse("غير مصرح لك بإضافة موظفين لهذا الحساب"));

            try
            {
                await _portalService.CreateTenantStaffAccountAsync(tenantId, dto);
                return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إنشاء حساب الموظف بنجاح"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<bool>.FailResponse(ex.Message));
            }
        }

        // 🆕 ب) عرض كافة الموظفين التابعين للمستأجر الحالي
        [HttpGet("staff/{tenantId}")]
        public async Task<IActionResult> GetMyStaff(int tenantId)
        {
            if (!ValidateCurrentUserTenant(tenantId))
                return StatusCode(403, ApiResponseDto<List<User>>.FailResponse("غير مصرح لك بعرض هذه البيانات"));

            var staff = await _portalService.GetMyStaffAsync(tenantId);
            return Ok(ApiResponseDto<List<User>>.SuccessResponse(staff));
        }
    }
}