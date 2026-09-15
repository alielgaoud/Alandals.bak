using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Reports;
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

        public DemandLettersController(DemandLetterPdfService pdfService)
        {
            _pdfService = pdfService;
        }

        /// <summary>
        /// توليد خطاب مطالبة مالية (إيجار + مصروفات محملة + رسوم)
        /// </summary>
        [HttpPost("pdf")]
        public async Task<IActionResult> GeneratePdf([FromBody] GenerateDemandLetterDto dto)
        {
            try
            {
                var bytes = await _pdfService.GenerateAsync(dto);
                var fileName = $"مطالبة_مالية_{dto.TenantId}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(bytes, "application/pdf", fileName);
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

        /// <summary>
        /// نسخة GET سريعة للاختبار
        /// </summary>
        [HttpGet("pdf")]
        public async Task<IActionResult> GeneratePdfGet(
            [FromQuery] int tenantId,
            [FromQuery] int? contractId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] DateTime? dueDate = null,
            [FromQuery] string? periodDescription = null)
        {
            var dto = new GenerateDemandLetterDto
            {
                TenantId = tenantId,
                ContractId = contractId,
                FromDate = fromDate,
                ToDate = toDate,
                DueDate = dueDate,
                PeriodDescription = periodDescription,
                IncludeRent = true,
                IncludeChargedExpenses = true,
                IncludeContractFees = true
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