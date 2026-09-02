using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Complaints;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class ComplaintsController : ControllerBase
    {
        private readonly IComplaintService _complaintService;

        public ComplaintsController(IComplaintService complaintService)
        {
            _complaintService = complaintService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? tenantId, [FromQuery] string? status)
        {
            var list = await _complaintService.GetAllComplaintsAsync(tenantId, status);
            return Ok(ApiResponseDto<List<ComplaintResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var complaint = await _complaintService.GetComplaintByIdAsync(id);
            if (complaint == null)
                return NotFound(ApiResponseDto<ComplaintResponseDto>.FailResponse("الشكوى غير موجودة"));

            return Ok(ApiResponseDto<ComplaintResponseDto>.SuccessResponse(complaint));
        }

        [HttpPost("{id}/reply")]
        public async Task<IActionResult> Reply(int id, [FromBody] CreateReplyDto dto)
        {
            try
            {
                int adminUserId = 1;
                var reply = await _complaintService.ReplyToComplaintAsync(id, adminUserId, dto);
                return Ok(ApiResponseDto<ComplaintReplyDto>.SuccessResponse(reply, "تم إرسال الرد بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<ComplaintReplyDto>.FailResponse(ex.Message));
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] ComplaintStatus status)
        {
            var result = await _complaintService.UpdateStatusAsync(id, status.ToString());
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الشكوى غير موجودة"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تحديث حالة الشكوى بنجاح"));
        }

        [HttpGet("report/pdf")]
        public async Task<IActionResult> DownloadReport(
       [FromQuery] int? tenantId,
       [FromQuery] string? status,
       [FromQuery] DateTime? fromDate,
       [FromQuery] DateTime? toDate,
       [FromQuery] bool summaryOnly = false) // 👈 تمرير معلمة التقرير التجميعي
        {
            var pdfService = HttpContext.RequestServices.GetRequiredService<ComplaintReportPdfService>();
            var pdfBytes = await pdfService.GenerateComplaintsReportPdfAsync(tenantId, status, fromDate, toDate, summaryOnly);

            string fileName = summaryOnly
                ? $"تقرير_الشكاوى_التجميعي_{DateTime.Now:yyyyMMdd}.pdf"
                : $"تقرير_الشكاوى_التفصيلي_{DateTime.Now:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}