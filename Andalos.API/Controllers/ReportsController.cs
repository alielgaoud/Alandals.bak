using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Reports;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // 1. لوحة المؤشرات اللحظية (Dashboard KPIs)
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var stats = await _reportService.GetDashboardStatsAsync();
            return Ok(ApiResponseDto<DashboardStatsDto>.SuccessResponse(stats));
        }

        // 2. تقرير الأداء المالي السنوي (أشهر السنة)
        [HttpGet("financial-performance/{year}")]
        public async Task<IActionResult> GetFinancialPerformance(int year)
        {
            var data = await _reportService.GetAnnualFinancialPerformanceAsync(year);
            return Ok(ApiResponseDto<List<MonthlyFinancialBarDto>>.SuccessResponse(data));
        }

        // 3. تقرير المتأخرات والمستحقات غير المسددة
        [HttpGet("overdue")]
        public async Task<IActionResult> GetOverdueReport()
        {
            var data = await _reportService.GetOverdueReportAsync();
            return Ok(ApiResponseDto<List<OverdueReportItemDto>>.SuccessResponse(data));
        }

        // 4. تقرير حالة إشغال وشغور المحلات
        [HttpGet("occupancy")]
        public async Task<IActionResult> GetOccupancyReport()
        {
            var data = await _reportService.GetUnitsOccupancyReportAsync();
            return Ok(ApiResponseDto<List<UnitOccupancyReportDto>>.SuccessResponse(data));
        }

        // 5. تقرير حركة الزوار والبوابات لفترة محددة
        [HttpGet("visitor-traffic")]
        public async Task<IActionResult> GetVisitorTraffic([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var fromDate = from ?? DateTime.Today.AddDays(-7);
            var toDate = to ?? DateTime.Today;
            var data = await _reportService.GetVisitorTrafficReportAsync(fromDate, toDate);
            return Ok(ApiResponseDto<List<DailyVisitorTrafficDto>>.SuccessResponse(data));
        }        // 6. تقرير الإيرادات المفلتر
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenueReport(
            [FromQuery] int? unitId,
            [FromQuery] int? tenantId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var data = await _reportService.GetRevenueReportAsync(unitId, tenantId, from, to);
            return Ok(ApiResponseDto<List<RevenueReportItemDto>>.SuccessResponse(data));
        }

        // 7. تقرير المصروفات المفلتر
        [HttpGet("expenses")]
        public async Task<IActionResult> GetExpensesReport(
            [FromQuery] int? unitId,
            [FromQuery] int? tenantId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var data = await _reportService.GetExpensesReportAsync(unitId, tenantId, from, to);
            return Ok(ApiResponseDto<List<ExpenseReportItemDto>>.SuccessResponse(data));
        }
    }
}