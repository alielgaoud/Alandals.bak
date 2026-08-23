using Andalos.API.DTOs.Reports;

namespace Andalos.API.Interfaces
{
    public interface IReportService
    {
        Task<DashboardStatsDto> GetDashboardStatsAsync();
        Task<List<MonthlyFinancialBarDto>> GetAnnualFinancialPerformanceAsync(int year);
        Task<List<OverdueReportItemDto>> GetOverdueReportAsync();
        Task<List<UnitOccupancyReportDto>> GetUnitsOccupancyReportAsync();
        Task<List<DailyVisitorTrafficDto>> GetVisitorTrafficReportAsync(DateTime fromDate, DateTime toDate);
    }
}