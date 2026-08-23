using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Expenses;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _service;

        public ExpensesController(IExpenseService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(ApiResponseDto<List<ExpenseResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("unit/{unitId}")]
        public async Task<IActionResult> GetByUnit(int unitId)
        {
            var list = await _service.GetByUnitAsync(unitId);
            return Ok(ApiResponseDto<List<ExpenseResponseDto>>.SuccessResponse(list));
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> Create([FromBody] CreateExpenseDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return Ok(ApiResponseDto<ExpenseResponseDto>.SuccessResponse(result, "تم تسجيل المصروف بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<ExpenseResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("سند الصرف غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف المصروف بنجاح"));
        }

        [HttpGet("total")]
        public async Task<IActionResult> GetTotal([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var total = await _service.GetTotalExpensesAsync(from, to);
            return Ok(ApiResponseDto<decimal>.SuccessResponse(total));
        }
    }
}