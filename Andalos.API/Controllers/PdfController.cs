using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PdfController : ControllerBase
    {
        private readonly ContractPdfService _contractPdf;
        private readonly ReceiptPdfService _receiptPdf;
        private readonly ReportPdfService _reportPdf;

        public PdfController(
            ContractPdfService contractPdf,
            ReceiptPdfService receiptPdf,
            ReportPdfService reportPdf)
        {
            _contractPdf = contractPdf;
            _receiptPdf = receiptPdf;
            _reportPdf = reportPdf;
        }

        // ===== عرض عقد PDF في المتصفح (للطباعة المباشرة) =====
        [HttpGet("contract/{id}/view")]
        public async Task<IActionResult> ViewContract(int id)
        {
            try
            {
                var pdfBytes = await _contractPdf.GenerateContractPdfAsync(id);
                return File(pdfBytes, "application/pdf"); // يعرض في المتصفح مباشرة
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // ===== تحميل عقد PDF =====
        [HttpGet("contract/{id}/download")]
        public async Task<IActionResult> DownloadContract(int id)
        {
            try
            {
                var pdfBytes = await _contractPdf.GenerateContractPdfAsync(id);
                return File(pdfBytes, "application/pdf", $"عقد_{id}.pdf");
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // ===== عرض سند قبض =====
        [HttpGet("receipt/{paymentId}/view")]
        public async Task<IActionResult> ViewReceipt(int paymentId)
        {
            try
            {
                var pdfBytes = await _receiptPdf.GenerateReceiptPdfAsync(paymentId);
                return File(pdfBytes, "application/pdf");
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // ===== تحميل سند قبض =====
        [HttpGet("receipt/{paymentId}/download")]
        public async Task<IActionResult> DownloadReceipt(int paymentId)
        {
            try
            {
                var pdfBytes = await _receiptPdf.GenerateReceiptPdfAsync(paymentId);
                return File(pdfBytes, "application/pdf", $"سند_قبض_{paymentId}.pdf");
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        // ===== تقرير المتأخرات PDF =====
        [HttpGet("report/overdue")]
        public async Task<IActionResult> OverdueReport()
        {
            var pdfBytes = await _reportPdf.GenerateOverdueReportPdfAsync();
            return File(pdfBytes, "application/pdf");
        }

        // ===== تقرير الإشغال PDF =====
        [HttpGet("report/occupancy")]
        public async Task<IActionResult> OccupancyReport()
        {
            var pdfBytes = await _reportPdf.GenerateOccupancyReportPdfAsync();
            return File(pdfBytes, "application/pdf");
        }

        // ===== التقرير المالي السنوي PDF =====
        [HttpGet("report/financial/{year}")]
        public async Task<IActionResult> FinancialReport(int year)
        {
            var pdfBytes = await _reportPdf.GenerateFinancialReportPdfAsync(year);
            return File(pdfBytes, "application/pdf");
        }
    }
}