using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Refunds;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RefundsController : ControllerBase
    {
        private readonly IRefundService _refundService;

        public RefundsController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _refundService.GetAllAsync();
            return Ok(ApiResponseDto<List<RefundResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("contract/{contractId}")]
        public async Task<IActionResult> GetByContract(int contractId)
        {
            var list = await _refundService.GetByContractAsync(contractId);
            return Ok(ApiResponseDto<List<RefundResponseDto>>.SuccessResponse(list));
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> Create([FromBody] CreateRefundDto dto)
        {
            try
            {
                var result = await _refundService.CreateAsync(dto);
                return Ok(ApiResponseDto<RefundResponseDto>.SuccessResponse(result, "تم تسجيل سند المرتجع المالي بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<RefundResponseDto>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<RefundResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _refundService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("سند المرتجع غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إلغاء سند المرتجع بنجاح"));
        }
    }
}