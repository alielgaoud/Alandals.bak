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
    }
}