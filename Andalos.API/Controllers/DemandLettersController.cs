using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Reports;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DemandLettersController : ControllerBase
    {
        private readonly DemandLetterPdfService _pdfService;
        private readonly INotificationService _notificationService; // 👈 لاستدعاء خدمة الإشعارات
        private readonly IWebHostEnvironment _env;

        public DemandLettersController(
            DemandLetterPdfService pdfService,
            INotificationService notificationService,
            IWebHostEnvironment env)
        {
            _pdfService = pdfService;
            _notificationService = notificationService;
            _env = env;
        }

        // =========================================================
        // 1) توليد وتحميل الـ PDF فقط (بدون إرسال إشعار)
        // =========================================================
        [HttpPost("pdf")]
        public async Task<IActionResult> GeneratePdf([FromBody] GenerateDemandLetterDto dto)
        {
            try
            {
                var bytes = await _pdfService.GenerateAsync(dto);
                var fileName = $"مطالبة_مالية_{dto.TenantId}_{DateTime.Now:yyyyMMddHHmm}.pdf";
                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // =========================================================
        // 2) 👈 جديد: توليد المطالبة وحفظها وإرسالها كإشعار للمستأجر
        // =========================================================
        [HttpPost("send")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> SendDemand([FromBody] SendDemandLetterDto dto)
        {
            try
            {
                // أ) توليد ملف הـ PDF
                var pdfBytes = await _pdfService.GenerateAsync(dto);

                // ب) حفظ الملف في مجلد /wwwroot/uploads/demands للرجوع إليه لاحقاً
                string webRoot = _env.WebRootPath;
                if (string.IsNullOrWhiteSpace(webRoot))
                    webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                string folder = Path.Combine(webRoot, "uploads", "demands");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = $"Demand_T{dto.TenantId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                string fullPath = Path.Combine(folder, fileName);

                await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

                // الرابط المباشر الذي سيفتح عند ضغط المستأجر على الإشعار
                string relativeUrl = $"/uploads/demands/{fileName}";

                // ج) إرسال إشعار فوري للمستأجر عبر النظام (In-App + Push)
                if (dto.SendNotification)
                {
                    string title = string.IsNullOrWhiteSpace(dto.NotificationTitle)
                        ? "⚠️ إشعار بمطالبة مالية جديدة"
                        : dto.NotificationTitle;

                    string message = string.IsNullOrWhiteSpace(dto.NotificationMessage)
                        ? $"تم إصدار مطالبة مالية بحقكم يرجى التفضل بالاطلاع عليها وسدادها قبل الموعد المحدد."
                        : dto.NotificationMessage;

                    if (dto.DueDate.HasValue && string.IsNullOrWhiteSpace(dto.NotificationMessage))
                        message += $" (تاريخ الاستحقاق: {dto.DueDate:yyyy/MM/dd})";

                    await _notificationService.SendToTenantAsync(
                        tenantId: dto.TenantId,
                        title: title,
                        message: message,
                        type: NotificationType.PaymentOverdue,
                        actionUrl: relativeUrl, // 👈 رابط المستند
                        relatedEntityId: dto.ContractId
                    );
                }

                return Ok(ApiResponseDto<object>.SuccessResponse(new
                {
                    fileUrl = relativeUrl,
                    fileName = fileName,
                    notificationSent = dto.SendNotification,
                    tenantId = dto.TenantId
                }, "تم إنشاء المطالبة وإرسالها كإشعار للمستأجر بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<string>.FailResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // =========================================================
        // 3) نسخة GET سريعة لاختبار الـ PDF من المتصفح مباشرة
        // =========================================================
        [HttpGet("pdf")]
        public async Task<IActionResult> GeneratePdfGet(
            [FromQuery] int tenantId,
            [FromQuery] int? contractId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] DateTime? dueDate = null,
            [FromQuery] string? periodDescription = null,
            [FromQuery] bool includeRent = true,
            [FromQuery] bool includeChargedExpenses = true,
            [FromQuery] bool includeContractFees = true)
        {
            var dto = new GenerateDemandLetterDto
            {
                TenantId = tenantId,
                ContractId = contractId,
                FromDate = fromDate,
                ToDate = toDate,
                DueDate = dueDate,
                PeriodDescription = periodDescription,
                IncludeRent = includeRent,
                IncludeChargedExpenses = includeChargedExpenses,
                IncludeContractFees = includeContractFees
            };

            try
            {
                var bytes = await _pdfService.GenerateAsync(dto);
                var fileName = $"مطالبة_مالية_{tenantId}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }
    }
}