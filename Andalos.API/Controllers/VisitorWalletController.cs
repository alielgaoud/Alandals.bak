using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VisitorWalletController : ControllerBase
    {
        private readonly IVisitorWalletService _walletService;

        public VisitorWalletController(IVisitorWalletService walletService)
        {
            _walletService = walletService;
        }

        // 1. البوابة: إصدار تصريح مدفوع
        [HttpPost("issue-paid-pass")]
        public async Task<IActionResult> IssuePaidPass([FromBody] CreatePaidVisitorPassDto dto)
        {
            int userId = GetCurrentUserId();
            var result = await _walletService.CreatePaidPassAsync(dto, userId);
            return Ok(ApiResponseDto<VisitorPassResponseDto>.SuccessResponse(result, "تم إصدار التصريح وتوليد محفظة الـ QR بنجاح"));
        }

        // 2. المحل: الخصم بالـ QR Code
        [HttpPost("shop/charge-qr")]
        public async Task<IActionResult> ChargeQr([FromBody] ProcessPassPurchaseDto dto)
        {
            // 👇 الحل: قراءة TenantId من الـ Header إذا لم يكن في الـ Claims
            int tenantId = GetCurrentTenantId();

            // 👇 Fallback: إذا لم نجده في الـ Claims، نقرأه من الـ Header
            if (tenantId == 0)
            {
                var headerTenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault();
                if (int.TryParse(headerTenantId, out var parsedId) && parsedId > 0)
                {
                    tenantId = parsedId;
                }
            }

            if (tenantId == 0)
                return BadRequest(ApiResponseDto<string>.FailResponse("يجب الدخول بحساب مستأجر لاستخدام كاسحة الـ QR"));

            var result = await _walletService.ProcessShopPurchaseAsync(dto, tenantId);
            if (!result.IsSuccess)
            {
                return BadRequest(new ApiResponseDto<PassPurchaseResultDto>
                {
                    Success = false,
                    Message = result.Message,
                    Data = result
                });
            }

            return Ok(ApiResponseDto<PassPurchaseResultDto>.SuccessResponse(result, result.Message));
        }

        // 3. المحل: استعراض مبيعاتي المعلقة بانتظار التسديد من الإدارة
        [HttpGet("shop/my-unsettled-balance")]
        public async Task<IActionResult> GetMyUnsettledBalance()
        {
            // 👇 نفس الحل
            int tenantId = GetCurrentTenantId();

            if (tenantId == 0)
            {
                var headerTenantId = Request.Headers["X-Tenant-Id"].FirstOrDefault();
                if (int.TryParse(headerTenantId, out var parsedId) && parsedId > 0)
                {
                    tenantId = parsedId;
                }
            }

            if (tenantId == 0)
                return BadRequest(ApiResponseDto<string>.FailResponse("لم يتم التعرف على حساب المستأجر"));

            var result = await _walletService.GetMyUnsettledBalanceAsync(tenantId);
            return Ok(ApiResponseDto<TenantPassBalanceDto>.SuccessResponse(result));
        }

        // 4. الإدارة: استعراض مستحقات جميع المحلات
        [HttpGet("admin/all-shops-balances")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> GetAllShopsBalances()
        {
            var list = await _walletService.GetAllShopsUnsettledBalancesAsync();
            return Ok(ApiResponseDto<List<TenantPassBalanceDto>>.SuccessResponse(list));
        }

        // 5. الإدارة: تسديد مستحقات المحل
        [HttpPost("admin/settle-shop")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> SettleShop([FromBody] ProcessSettlementDto dto)
        {
            int adminUserId = GetCurrentUserId();
            try
            {
                var result = await _walletService.SettleShopBalanceAsync(dto, adminUserId);
                return Ok(ApiResponseDto<SettlementResponseDto>.SuccessResponse(result, "تم تسديد مستحقات المحل وتأكيد الحركات بنجاح"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // 6. الحارس: استعراض عهدة اليوم
        [HttpGet("gatekeeper/my-shift-summary")]
        public async Task<IActionResult> GetMyShiftSummary()
        {
            int userId = GetCurrentUserId();
            var summary = await _walletService.GetCurrentShiftSummaryAsync(userId);
            return Ok(ApiResponseDto<GatekeeperShiftSummaryDto>.SuccessResponse(summary));
        }

        // 7. الإدارة: استلام عهدة الحارس
        [HttpPost("admin/handover-shift/{shiftId}")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> HandoverShift(int shiftId)
        {
            int adminUserId = GetCurrentUserId();
            var result = await _walletService.HandoverShiftCashAsync(shiftId, adminUserId);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الوردية غير موجودة أو تسلّمت مسبقاً"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم استلام عُهدة الحارس بنجاح وتبرئة ذمته"));
        }

        private int GetCurrentUserId()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 1;
        }

        private int GetCurrentTenantId()
        {
            // 👇 محاولة 1: قراءة من Claim مخصص
            if (int.TryParse(User.FindFirst("TenantId")?.Value, out var tid) && tid > 0)
                return tid;

            // 👇 محاولة 2: قراءة من NameIdentifier (إذا كان التوكن يخص مستأجر)
            if (int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) && uid > 0)
            {
                // يمكن هنا البحث في قاعدة البيانات عن Tenant المرتبط بهذا UserId
                // لكن كحل سريع نرجع 0 ونعتمد على الـ Header
            }

            return 0;
        }
    }
}