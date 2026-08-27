using Andalos.API.Data;
using Andalos.API.DTOs.Reports;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Andalos.API.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _db;

        public ReportService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DashboardStatsDto> GetDashboardStatsAsync()
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfYear = new DateTime(now.Year, 1, 1);
            var thirtyDaysFromNow = today.AddDays(30);

            // 1. إحصائيات المحلات
            var totalUnits = await _db.Units.CountAsync(u => u.IsActive);
            var rentedUnits = await _db.Units.CountAsync(u => u.IsActive && u.Status == UnitStatus.Rented);
            var vacantUnits = await _db.Units.CountAsync(u => u.IsActive && u.Status == UnitStatus.Vacant);
            var maintenanceUnits = await _db.Units.CountAsync(u => u.IsActive && u.Status == UnitStatus.Maintenance);
            decimal occupancyRate = totalUnits > 0 ? Math.Round(((decimal)rentedUnits / totalUnits) * 100, 1) : 0;

            // 2. إحصائيات العقود
            var activeContractsCount = await _db.Contracts.CountAsync(c => c.IsActive && c.Status == ContractStatus.Active);
            var expiringSoonContracts = await _db.Contracts.CountAsync(c => c.IsActive
                && c.Status == ContractStatus.Active
                && c.EndDate >= today
                && c.EndDate <= thirtyDaysFromNow);

            // 3. الإيرادات
            var thisMonthRevenue = await _db.Payments
                .Where(p => p.IsActive && p.PaymentDate >= startOfMonth)
                .SumAsync(p => p.Amount);

            var ytdRevenue = await _db.Payments
                .Where(p => p.IsActive && p.PaymentDate >= startOfYear)
                .SumAsync(p => p.Amount);

            // 4. المصروفات
            var thisMonthExpenses = await _db.Expenses
                .Where(e => e.IsActive && e.ExpenseDate >= startOfMonth)
                .SumAsync(e => e.Amount);

            var ytdExpenses = await _db.Expenses
                .Where(e => e.IsActive && e.ExpenseDate >= startOfYear)
                .SumAsync(e => e.Amount);

            // 5. حساب إجمالي المتأخرات
            var activeContracts = await _db.Contracts
                .Where(c => c.IsActive && c.Status == ContractStatus.Active)
                .Select(c => new { c.Id, c.StartDate, c.RentAmount })
                .ToListAsync();

            decimal totalOverdue = 0;
            foreach (var contract in activeContracts)
            {
                int monthsElapsed = (int)((today - contract.StartDate).TotalDays / 30);
                if (monthsElapsed < 1) monthsElapsed = 1;
                decimal due = monthsElapsed * contract.RentAmount;

                decimal paid = await _db.Payments
                    .Where(p => p.ContractId == contract.Id && p.IsActive)
                    .SumAsync(p => p.Amount);

                if (due > paid)
                {
                    totalOverdue += (due - paid);
                }
            }

            // 6. إحصائيات الزوار اليوم
            var todayPasses = await _db.VisitorPasses.CountAsync(p => p.IsActive && p.ValidDate.Date == today);
            var todayAllowed = await _db.EntryLogs.CountAsync(e => e.ScanTime.Date == today && e.IsAllowed);
            var todayRejected = await _db.EntryLogs.CountAsync(e => e.ScanTime.Date == today && !e.IsAllowed);

            // 7. طلبات الصيانة المعلقة
            var pendingMaintenance = await _db.MaintenanceRequests
                .CountAsync(m => m.IsActive && (m.Status == MaintenanceStatus.New || m.Status == MaintenanceStatus.InProgress));

            return new DashboardStatsDto
            {
                TotalUnits = totalUnits,
                RentedUnits = rentedUnits,
                VacantUnits = vacantUnits,
                MaintenanceUnits = maintenanceUnits,
                OccupancyRate = occupancyRate,
                ActiveContractsCount = activeContractsCount,
                ExpiringSoonContractsCount = expiringSoonContracts,
                ThisMonthRevenue = thisMonthRevenue,
                ThisMonthExpenses = thisMonthExpenses,
                ThisMonthNetIncome = thisMonthRevenue - thisMonthExpenses,
                YearToDateRevenue = ytdRevenue,
                YearToDateExpenses = ytdExpenses,
                YearToDateNetIncome = ytdRevenue - ytdExpenses,
                TotalOverdueAmount = totalOverdue,
                TodayPassesCreated = todayPasses,
                TodayScansAllowed = todayAllowed,
                TodayScansRejected = todayRejected,
                PendingMaintenanceCount = pendingMaintenance
            };
        }

        public async Task<List<MonthlyFinancialBarDto>> GetAnnualFinancialPerformanceAsync(int year)
        {
            var result = new List<MonthlyFinancialBarDto>();
            var culture = new CultureInfo("ar-LY");

            var payments = await _db.Payments
                .Where(p => p.IsActive && p.PaymentDate.Year == year)
                .ToListAsync();

            var expenses = await _db.Expenses
                .Where(e => e.IsActive && e.ExpenseDate.Year == year)
                .ToListAsync();

            for (int month = 1; month <= 12; month++)
            {
                decimal rev = payments.Where(p => p.PaymentDate.Month == month).Sum(p => p.Amount);
                decimal exp = expenses.Where(e => e.ExpenseDate.Month == month).Sum(e => e.Amount);

                result.Add(new MonthlyFinancialBarDto
                {
                    Month = month,
                    MonthName = culture.DateTimeFormat.GetMonthName(month),
                    Revenue = rev,
                    Expenses = exp,
                    NetProfit = rev - exp
                });
            }

            return result;
        }

        public async Task<List<OverdueReportItemDto>> GetOverdueReportAsync()
        {
            var today = DateTime.Today;
            var contracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.Unit)
                .Where(c => c.IsActive && c.Status == ContractStatus.Active)
                .ToListAsync();

            var list = new List<OverdueReportItemDto>();

            foreach (var c in contracts)
            {
                int monthsElapsed = (int)((today - c.StartDate).TotalDays / 30);
                if (monthsElapsed < 1) monthsElapsed = 1;
                decimal due = monthsElapsed * c.RentAmount;

                var payments = await _db.Payments
                    .Where(p => p.ContractId == c.Id && p.IsActive)
                    .ToListAsync();

                decimal paid = payments.Sum(p => p.Amount);

                if (due > paid)
                {
                    list.Add(new OverdueReportItemDto
                    {
                        ContractId = c.Id,
                        ContractNumber = c.ContractNumber,
                        TenantName = c.Tenant?.FullName ?? "",
                        TenantPhone = c.Tenant?.Phone ?? "",
                        UnitNumber = c.Unit?.UnitNumber ?? "",
                        MonthlyRent = c.RentAmount,
                        TotalDue = due,
                        TotalPaid = paid,
                        RemainingAmount = due - paid,
                        ContractStartDate = c.StartDate,
                        ContractEndDate = c.EndDate,
                        LastPaymentDate = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate
                    });
                }
            }

            return list.OrderByDescending(x => x.RemainingAmount).ToList();
        }

        public async Task<List<UnitOccupancyReportDto>> GetUnitsOccupancyReportAsync()
        {
            var units = await _db.Units.Where(u => u.IsActive).ToListAsync();
            var activeContracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Where(c => c.IsActive && c.Status == ContractStatus.Active)
                .ToListAsync();

            var report = new List<UnitOccupancyReportDto>();

            foreach (var unit in units)
            {
                var contract = activeContracts.FirstOrDefault(c => c.UnitId == unit.Id);

                report.Add(new UnitOccupancyReportDto
                {
                    UnitId = unit.Id,
                    UnitNumber = unit.UnitNumber,
                    UnitName = unit.UnitName,
                    ActivityType = unit.ActivityType.ToString(), // 👈 تم تصحيح الخطأ الثاني هنا بتغيير اسم الحقل المستقبل في الـ DTO إلى ActivityType
                    Status = unit.Status.ToString(),
                    Area = unit.Area,
                    CurrentTenantName = contract?.Tenant?.FullName,
                    CurrentTenantPhone = contract?.Tenant?.Phone,
                    CurrentRentAmount = contract?.RentAmount,
                    ContractEndDate = contract?.EndDate
                });
            }

            return report.OrderBy(u => u.UnitNumber).ToList();
        }
        public async Task<List<DailyVisitorTrafficDto>> GetVisitorTrafficReportAsync(DateTime fromDate, DateTime toDate)
        {
            var passes = await _db.VisitorPasses
                .Where(p => p.IsActive && p.ValidDate.Date >= fromDate.Date && p.ValidDate.Date <= toDate.Date)
                .ToListAsync();

            var logs = await _db.EntryLogs
                .Where(e => e.ScanTime.Date >= fromDate.Date && e.ScanTime.Date <= toDate.Date)
                .ToListAsync();

            var list = new List<DailyVisitorTrafficDto>();

            for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
            {
                list.Add(new DailyVisitorTrafficDto
                {
                    Date = date,
                    TotalPasses = passes.Count(p => p.ValidDate.Date == date),
                    AllowedEntries = logs.Count(l => l.ScanTime.Date == date && l.IsAllowed),
                    RejectedEntries = logs.Count(l => l.ScanTime.Date == date && !l.IsAllowed)
                });
            }

            return list;
        }


        // =========================================================================
        // 👈 تقرير الإيرادات المفلتر (Payments Report)
        // =========================================================================
        public async Task<List<RevenueReportItemDto>> GetRevenueReportAsync(
            int? unitId,
            int? tenantId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _db.Payments
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Tenant)
                .Include(p => p.Contract)
                    .ThenInclude(c => c!.Unit)
                .Where(p => p.IsActive);

            // تطبيق فلاتر البحث ديناميكياً
            if (unitId.HasValue)
                query = query.Where(p => p.UnitId == unitId.Value);

            if (tenantId.HasValue)
                query = query.Where(p => p.TenantId == tenantId.Value);

            if (fromDate.HasValue)
                query = query.Where(p => p.PaymentDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(p => p.PaymentDate <= toDate.Value.Date);

            return await query
                .OrderByDescending(p => p.PaymentDate)
                .Select(p => new RevenueReportItemDto
                {
                    PaymentId = p.Id,
                    ReceiptNumber = p.ReceiptNumber,
                    ContractNumber = p.Contract != null ? p.Contract.ContractNumber : "",
                    TenantName = p.Contract != null && p.Contract.Tenant != null ? p.Contract.Tenant.FullName : "مستأجر محذوف",
                    UnitNumber = p.Contract != null && p.Contract.Unit != null ? p.Contract.Unit.UnitNumber : "",
                    PaymentType = p.PaymentType.ToString(),
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod.ToString(),
                    ReferenceNumber = p.ReferenceNumber,
                    PaymentDate = p.PaymentDate,
                    Notes = p.Notes
                })
                .ToListAsync();
        }

        // =========================================================================
        // 👈 تقرير المصروفات المفلتر (Expenses Report)
        // =========================================================================
        public async Task<List<ExpenseReportItemDto>> GetExpensesReportAsync(
            int? unitId,
            int? tenantId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _db.Expenses
                .Include(e => e.Unit)
                .Include(e => e.Tenant)
                .Where(e => e.IsActive);

            // تطبيق فلاتر البحث ديناميكياً
            if (unitId.HasValue)
                query = query.Where(e => e.UnitId == unitId.Value);

            if (tenantId.HasValue)
                query = query.Where(e => e.TenantId == tenantId.Value);

            if (fromDate.HasValue)
                query = query.Where(e => e.ExpenseDate >= fromDate.Value.Date);

            if (toDate.HasValue)
                query = query.Where(e => e.ExpenseDate <= toDate.Value.Date);

            return await query
                .OrderByDescending(e => e.ExpenseDate)
                .Select(e => new ExpenseReportItemDto
                {
                    ExpenseId = e.Id,
                    ExpenseNumber = e.ExpenseNumber,
                    UnitNumber = e.Unit != null ? e.Unit.UnitNumber : null,
                    UnitName = e.Unit != null ? e.Unit.UnitName : null,
                    TenantName = e.Tenant != null ? e.Tenant.FullName : null,
                    IsChargedToTenant = e.IsChargedToTenant,
                    ExpenseType = e.ExpenseType.ToString(),
                    Amount = e.Amount,
                    ExpenseDate = e.ExpenseDate,
                    PaidTo = e.PaidTo,
                    Description = e.Description,
                    InvoiceNumber = e.InvoiceNumber,
                    AttachmentUrl = e.AttachmentUrl
                })
                .ToListAsync();
        }
    }
}