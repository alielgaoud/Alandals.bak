using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Units;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UnitsController : ControllerBase
    {
        private readonly IUnitService _unitService;

        public UnitsController(IUnitService unitService)
        {
            _unitService = unitService;
        }

        // GET: api/units
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var units = await _unitService.GetAllAsync();
            return Ok(ApiResponseDto<List<UnitResponseDto>>.SuccessResponse(units));
        }

        // GET: api/units/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var unit = await _unitService.GetByIdAsync(id);
            if (unit == null)
                return NotFound(ApiResponseDto<UnitResponseDto>.FailResponse("المحل غير موجود"));

            return Ok(ApiResponseDto<UnitResponseDto>.SuccessResponse(unit));
        }

        // POST: api/units
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateUnitDto dto)
        {
            try
            {
                var unit = await _unitService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = unit.Id },
                    ApiResponseDto<UnitResponseDto>.SuccessResponse(unit, "تم إنشاء المحل بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<UnitResponseDto>.FailResponse(ex.Message));
            }
        }

        // PUT: api/units/5
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUnitDto dto)
        {
            var unit = await _unitService.UpdateAsync(id, dto);
            if (unit == null)
                return NotFound(ApiResponseDto<UnitResponseDto>.FailResponse("المحل غير موجود"));

            return Ok(ApiResponseDto<UnitResponseDto>.SuccessResponse(unit, "تم تحديث المحل بنجاح"));
        }

        // DELETE: api/units/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _unitService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("المحل غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف المحل بنجاح"));
        }

        // GET: api/units/count/Rented
        [HttpGet("count/{status}")]
        public async Task<IActionResult> GetCountByStatus(string status)
        {
            var count = await _unitService.GetCountByStatusAsync(status);
            return Ok(ApiResponseDto<int>.SuccessResponse(count));
        }
    }
}