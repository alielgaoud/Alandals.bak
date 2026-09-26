using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Maintenance;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MaintenanceController : ControllerBase
    {
        private readonly IMaintenanceService _service;

        public MaintenanceController(IMaintenanceService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(ApiResponseDto<List<MaintenanceResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null)
                return NotFound(ApiResponseDto<MaintenanceResponseDto>.FailResponse("طلب الصيانة غير موجود"));

            return Ok(ApiResponseDto<MaintenanceResponseDto>.SuccessResponse(item));
        }

        [HttpGet("unit/{unitId}")]
        public async Task<IActionResult> GetByUnit(int unitId)
        {
            var list = await _service.GetByUnitAsync(unitId);
            return Ok(ApiResponseDto<List<MaintenanceResponseDto>>.SuccessResponse(list));
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateMaintenanceRequestDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id },
                    ApiResponseDto<MaintenanceResponseDto>.SuccessResponse(result, "تم تسجيل طلب الصيانة بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<MaintenanceResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateMaintenanceStatusDto dto)
        {
            var result = await _service.UpdateStatusAsync(id, dto);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("طلب الصيانة غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تحديث حالة الصيانة بنجاح"));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("طلب الصيانة غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الطلب بنجاح"));
        }

        // =========================================================================
        // 👈 جديد: تحميل المستأجر (الفوترة) — يدوياً أو كإجراء مستقل بعد الإكمال
        // =========================================================================
        [HttpPost("{id}/charge")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> ChargeTenant(int id, [FromBody] ChargeMaintenanceDto dto)
        {
            try
            {
                var result = await _service.ChargeTenantAsync(id, dto);
                return Ok(ApiResponseDto<MaintenanceResponseDto>.SuccessResponse(result,
                    $"تم تحميل مبلغ {dto.BilledAmount:N2} د.ل على المستأجر بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<MaintenanceResponseDto>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<MaintenanceResponseDto>.FailResponse(ex.Message));
            }
        }

        // 👈 جديد: قائمة التحميلات (اختياري: تصفية بالمستأجر / غير المسدد فقط)
        [HttpGet("charges")]
        public async Task<IActionResult> GetCharges([FromQuery] int? tenantId, [FromQuery] bool? unsettledOnly)
        {
            var list = await _service.GetChargesAsync(tenantId, unsettledOnly);
            return Ok(ApiResponseDto<List<TenantChargeDto>>.SuccessResponse(list));
        }

        // 👈 جديد: تفاصيل تحميل
        [HttpGet("charges/{id}")]
        public async Task<IActionResult> GetCharge(int id)
        {
            var item = await _service.GetChargeAsync(id);
            if (item == null)
                return NotFound(ApiResponseDto<TenantChargeDto>.FailResponse("التحميل غير موجود"));

            return Ok(ApiResponseDto<TenantChargeDto>.SuccessResponse(item));
        }

        // 👈 جديد: سداد التحميل المستحق (تحصيل الإيراد — نقدي أو تحويل)
        [HttpPost("charges/{id}/settle")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> SettleCharge(int id, [FromBody] SettleChargeDto dto)
        {
            try
            {
                var result = await _service.SettleChargeAsync(id, dto);
                return Ok(ApiResponseDto<TenantChargeDto>.SuccessResponse(result,
                    $"تم تحصيل {result.SettledAmount:N2} د.ل وتسجيله كإيراد صيانة بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<TenantChargeDto>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<TenantChargeDto>.FailResponse(ex.Message));
            }
        }
    }
}