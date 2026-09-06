using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantAccountsController : ControllerBase
    {
        private readonly ITenantAccountService _accountService;

        public TenantAccountsController(ITenantAccountService accountService)
        {
            _accountService = accountService;
        }

        // 1. كشف حساب المستأجر
        [HttpGet("{tenantId}/statement")]
        public async Task<IActionResult> GetStatement(int tenantId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var statement = await _accountService.GetStatementAsync(tenantId, fromDate, toDate);
            if (statement == null)
                return NotFound(ApiResponseDto<string>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<TenantAccountStatementDto>.SuccessResponse(statement));
        }

        // 2. أرصدة جميع المستأجرين
        [HttpGet("overview")]
        public async Task<IActionResult> GetAllBalances()
        {
            var balances = await _accountService.GetAllTenantsBalancesAsync();
            return Ok(ApiResponseDto<List<TenantBalanceOverviewDto>>.SuccessResponse(balances));
        }

        // 3. 👈 جديد: إيداع مبلغ في محفظة المستأجر (دفعة مقدمة)
        [HttpPost("{tenantId}/deposit-advance")]
        public async Task<IActionResult> DepositAdvance(int tenantId, [FromBody] DepositAdvancePaymentDto dto)
        {
            try
            {
                var payment = await _accountService.DepositAdvancePaymentAsync(tenantId, dto.Amount, dto.PaymentMethod, dto.Notes ?? "");
                return Ok(ApiResponseDto<Payment>.SuccessResponse(payment, "تم إيداع الدفعة المقدمة في رصيد المستأجر بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // 4. 👈 جديد: تشغيل الخصم والتسوية الشهرية الآلية
        [HttpPost("process-monthly-dues")]
        public async Task<IActionResult> ProcessMonthlyDues()
        {
            await _accountService.ProcessMonthlyRentDuesAsync();
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تمت معالجة الخصومات الشهرية من أرصدة المستأجرين بنجاح"));
        }
    }
}