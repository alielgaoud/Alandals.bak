using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BankTransfersController : ControllerBase
    {
        private readonly IBankTransferService _bankTransferService;

        // 👈 حقن الخدمة عبر المشيد Constructor
        public BankTransfersController(IBankTransferService bankTransferService)
        {
            _bankTransferService = bankTransferService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPending()
        {
            var list = await _bankTransferService.GetRequestsAsync(TransferRequestStatus.Pending);
            return Ok(ApiResponseDto<List<TransferRequestResponseDto>>.SuccessResponse(list));
        }

        [HttpPost("{id}/review")]
        public async Task<IActionResult> Review(int id, [FromBody] ReviewTransferRequestDto dto)
        {
            try
            {
                var result = await _bankTransferService.ReviewRequestAsync(id, dto);
                return Ok(ApiResponseDto<TransferRequestResponseDto>.SuccessResponse(result, "تمت مراجعة الطلب وتحديث الحسابات."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }
    }
}