using Andalos.API.Security;
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
            int tenantId = GetCurrentTenantId();
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
            int tenantId = GetCurrentTenantId();
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
            catch (Exception ex) when (ex is not Andalos.API.Security.ForbiddenOperationException and not Andalos.API.Security.ConcurrencyConflictException and not Microsoft.EntityFrameworkCore.DbUpdateException and not System.Data.Common.DbException)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse("تعذر تنفيذ التسوية."));
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

        /// <summary>
        /// تقرير المبالغ المستلمة في البوابة (تفصيلي + ملخص ورديات)
        /// فلاتر: gatekeeperUserId, fromDate, toDate, isHandedOver
        /// </summary>
        [HttpGet("admin/gate-cash-report")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> GetGateCashReport(
            [FromQuery] int? gatekeeperUserId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool? isHandedOver)
        {
            var report = await _walletService.GetGateCashReportAsync(
                gatekeeperUserId,
                fromDate,
                toDate,
                isHandedOver);

            return Ok(ApiResponseDto<GateCashReportSummaryDto>.SuccessResponse(report));
        }

        /// <summary>
        /// نسخة مختصرة: فقط تفاصيل الإيصالات المستلمة في البوابة
        /// </summary>
        [HttpGet("admin/gate-cash-receipts")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> GetGateCashReceipts(
            [FromQuery] int? gatekeeperUserId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            var report = await _walletService.GetGateCashReportAsync(
                gatekeeperUserId,
                fromDate,
                toDate,
                null);

            return Ok(ApiResponseDto<object>.SuccessResponse(new
            {
                totalCount = report.TotalReceiptsCount,
                totalCashCollected = report.TotalCashCollected,
                receipts = report.Receipts
            }));
        }

        /// <summary>
        /// تقرير ورديات الحراس فقط
        /// </summary>
        [HttpGet("admin/gate-shifts-report")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> GetGateShiftsReport(
            [FromQuery] int? gatekeeperUserId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool? isHandedOver)
        {
            var report = await _walletService.GetGateCashReportAsync(
                gatekeeperUserId,
                fromDate,
                toDate,
                isHandedOver);

            return Ok(ApiResponseDto<object>.SuccessResponse(new
            {
                totalShifts = report.TotalShiftsCount,
                openShifts = report.OpenShiftsCount,
                totalCashCollected = report.Shifts.Sum(s => s.TotalCashCollected),
                handedOverCash = report.TotalHandedOverCash,
                pendingHandoverCash = report.TotalPendingHandoverCash,
                shifts = report.Shifts
            }));
        }

        // GET: api/VisitorWallet/admin/tenant-history/1 (استعراض سجل محفظة مستأجر معين للإدارة)
        [HttpGet("admin/tenant-history/{tenantId}")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> GetTenantWalletHistoryForAdmin(
            int tenantId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool? isSettled)
        {
            var history = await _walletService.GetTenantWalletHistoryAsync(tenantId, fromDate, toDate, isSettled);
            return Ok(ApiResponseDto<TenantWalletFullHistoryDto>.SuccessResponse(history));
        }

        [HttpPost("gate/add-balance")]
        public async Task<IActionResult> AddBalance([FromBody] AddBalanceToPassDto dto)
        {
            var result = await _walletService.AddBalanceToPassAsync(dto, GetCurrentUserId());
            return Ok(ApiResponseDto<AddBalanceToPassResponseDto>.SuccessResponse(result));
        }
        private int GetCurrentUserId() => HttpContext.RequestServices.GetRequiredService<CurrentUser>().UserId;
        private int GetCurrentTenantId() => HttpContext.RequestServices.GetRequiredService<CurrentUser>().TenantId;
    }
}
