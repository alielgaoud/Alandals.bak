using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Complaints;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/tenant-complaints")]
    [Authorize]
    public class TenantComplaintsController : ControllerBase
    {
        private readonly IComplaintService _complaintService;

        public TenantComplaintsController(IComplaintService complaintService)
        {
            _complaintService = complaintService;
        }

        [HttpPost("{tenantId}")]
        public async Task<IActionResult> Submit(int tenantId, [FromBody] CreateComplaintDto dto)
        {
            try
            {
                int userId = 1;
                var result = await _complaintService.SubmitComplaintAsync(tenantId, userId, dto);
                return Ok(ApiResponseDto<TenantComplaintDto>.SuccessResponse(result, "تم إرسال الشكوى بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<TenantComplaintDto>.FailResponse(ex.Message));
            }
        }

        [HttpGet("{tenantId}")]
        public async Task<IActionResult> GetMyComplaints(int tenantId)
        {
            var list = await _complaintService.GetTenantComplaintsAsync(tenantId);
            return Ok(ApiResponseDto<List<TenantComplaintDto>>.SuccessResponse(list));
        }
    }
}