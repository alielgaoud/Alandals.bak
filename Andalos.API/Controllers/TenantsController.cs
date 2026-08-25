using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.DTOs.Payments;
using Andalos.API.DTOs.Tenants;
using Andalos.API.DTOs.Units;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        public TenantsController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        // ==================== CRUD الأساسي ====================

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenants = await _tenantService.GetAllAsync();
            return Ok(ApiResponseDto<List<TenantResponseDto>>.SuccessResponse(tenants));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tenant = await _tenantService.GetByIdAsync(id);
            if (tenant == null)
                return NotFound(ApiResponseDto<TenantResponseDto>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<TenantResponseDto>.SuccessResponse(tenant));
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateTenantDto dto)
        {
            try
            {
                var tenant = await _tenantService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = tenant.Id },
                    ApiResponseDto<TenantResponseDto>.SuccessResponse(tenant, "تم إضافة المستأجر بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<TenantResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantDto dto)
        {
            try
            {
                var tenant = await _tenantService.UpdateAsync(id, dto);
                if (tenant == null)
                    return NotFound(ApiResponseDto<TenantResponseDto>.FailResponse("المستأجر غير موجود"));

                return Ok(ApiResponseDto<TenantResponseDto>.SuccessResponse(tenant, "تم تحديث بيانات المستأجر بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<TenantResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _tenantService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف المستأجر بنجاح"));
        }

        // ==================== 7 Endpoints منفصلة ====================

        // 1. البيانات الشخصية فقط
        [HttpGet("{id}/info")]
        public async Task<IActionResult> GetPersonalInfo(int id)
        {
            var info = await _tenantService.GetPersonalInfoAsync(id);
            if (info == null)
                return NotFound(ApiResponseDto<TenantResponseDto>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<TenantResponseDto>.SuccessResponse(info));
        }

        // 2. المحلات المستأجرة فقط
        [HttpGet("{id}/units")]
        public async Task<IActionResult> GetRentedUnits(int id)
        {
            var units = await _tenantService.GetRentedUnitsAsync(id);
            return Ok(ApiResponseDto<List<UnitResponseDto>>.SuccessResponse(units));
        }

        // 3. العقود فقط
        [HttpGet("{id}/contracts")]
        public async Task<IActionResult> GetContracts(int id)
        {
            var contracts = await _tenantService.GetContractsAsync(id);
            return Ok(ApiResponseDto<List<ContractResponseDto>>.SuccessResponse(contracts));
        }

        // 4. الدفعات فقط
        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetPayments(int id)
        {
            var payments = await _tenantService.GetPaymentsAsync(id);
            return Ok(ApiResponseDto<List<PaymentResponseDto>>.SuccessResponse(payments));
        }

        // 5. الصيانة فقط
        [HttpGet("{id}/maintenance")]
        public async Task<IActionResult> GetMaintenance(int id)
        {
            var list = await _tenantService.GetMaintenanceRequestsAsync(id);
            return Ok(ApiResponseDto<List<MaintenanceResponseDto>>.SuccessResponse(list));
        }

        // 6. تصاريح الزوار فقط
        [HttpGet("{id}/visitor-passes")]
        public async Task<IActionResult> GetVisitorPasses(int id)
        {
            var passes = await _tenantService.GetVisitorPassesAsync(id);
            return Ok(ApiResponseDto<List<VisitorPassResponseDto>>.SuccessResponse(passes));
        }

        // 7. الملخص المالي فقط
        [HttpGet("{id}/financial-summary")]
        public async Task<IActionResult> GetFinancialSummary(int id)
        {
            var summary = await _tenantService.GetFinancialSummaryAsync(id);
            if (summary == null)
                return NotFound(ApiResponseDto<TenantFinancialSummaryDto>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<TenantFinancialSummaryDto>.SuccessResponse(summary));
        }
    }
}