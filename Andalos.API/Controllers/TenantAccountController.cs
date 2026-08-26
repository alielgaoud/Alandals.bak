using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public class TenantAccountController : ControllerBase
    {
        private readonly ITenantAccountService _accountService;

        public TenantAccountController(ITenantAccountService accountService)
        {
            _accountService = accountService;
        }

        // كشف حساب شامل لمستأجر واحد
        [HttpGet("statement/{tenantId}")]
        public async Task<IActionResult> GetStatement(int tenantId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var statement = await _accountService.GetStatementAsync(tenantId, from, to);
            if (statement == null)
                return NotFound(ApiResponseDto<TenantAccountStatementDto>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<TenantAccountStatementDto>.SuccessResponse(statement));
        }

        // ملخص أرصدة كل المستأجرين
        [HttpGet("balances")]
        public async Task<IActionResult> GetAllBalances()
        {
            var balances = await _accountService.GetAllTenantsBalancesAsync();
            return Ok(ApiResponseDto<List<TenantBalanceOverviewDto>>.SuccessResponse(balances));
        }
    }
}