using Andalos.API.Data;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Andalos.API.Services
{
    public interface ITenantAccountService
    {
        Task<TenantAccountStatementDto?> GetStatementAsync(int tenantId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<TenantBalanceOverviewDto>> GetAllTenantsBalancesAsync();
    }

    public class TenantAccountService : ITenantAccountService
    {
        private readonly AppDbContext _db;

        public TenantAccountService(AppDbContext db)
        {
            _db = db;
        }

        // =====================================================
        // كشف حساب شامل لمستأجر واحد (Bank Statement)
        // =====================================================
        public async Task<TenantAccountStatementDto?> GetStatementAsync(int tenantId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return null;

            var today = DateTime.Today;
            fromDate ??= new DateTime(today.Year - 2, 1, 1);
            toDate ??= today;

            var contracts = await _db.Contracts
                .Include(c => c.Unit)
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync();

            var payments = await _db.Payments
                .Include(p => p.Contract).ThenInclude(c => c!.Unit)
                .Where(p => p.TenantId == tenantId && p.IsActive
                    && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
                .ToListAsync();

            // ===== 1. بناء قائمة الحركات (Transactions) =====
            var transactions = new List<AccountTransactionDto>();

            // 1.a) إضافة المستحقات (Debit) - الإيجارات والتأمينات لكل عقد
            foreach (var contract in contracts.Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired))
            {
                var contractStart = contract.StartDate < fromDate.Value ? fromDate.Value : contract.StartDate;
                var contractEnd = contract.EndDate > toDate.Value ? toDate.Value : contract.EndDate;
                if (contractEnd > today) contractEnd = today;

                // أ) قيد التأمين (Deposit) عند بداية العقد
                if (contract.DepositAmount > 0 && contract.StartDate >= fromDate && contract.StartDate <= toDate)
                {
                    transactions.Add(new AccountTransactionDto
                    {
                        Id = -contract.Id * 1000,
                        ReferenceNumber = $"DEP-{contract.ContractNumber}",
                        TransactionDate = contract.StartDate,
                        TransactionType = "Debit",
                        Category = "Deposit",
                        CategoryLabel = "عربون / ضمان",
                        Description = $"مبلغ ضمان عقد {contract.ContractNumber}",
                        Debit = contract.DepositAmount,
                        Credit = 0,
                        ContractId = contract.Id,
                        ContractNumber = contract.ContractNumber,
                        UnitNumber = contract.Unit?.UnitNumber
                    });
                }

                // ب) قيود الإيجار الشهرية
                var currentMonth = new DateTime(contractStart.Year, contractStart.Month, contractStart.Day);
                int monthCounter = 0;
                while (currentMonth <= contractEnd && monthCounter < 240)
                {
                    transactions.Add(new AccountTransactionDto
                    {
                        Id = -contract.Id * 10000 - monthCounter,
                        ReferenceNumber = $"RENT-{contract.ContractNumber}-{currentMonth:yyyyMM}",
                        TransactionDate = currentMonth,
                        TransactionType = "Debit",
                        Category = "Rent",
                        CategoryLabel = "إيجار شهري",
                        Description = $"إيجار شهر {currentMonth:MM/yyyy} — {contract.Unit?.UnitName ?? ""}",
                        Debit = contract.RentAmount,
                        Credit = 0,
                        ContractId = contract.Id,
                        ContractNumber = contract.ContractNumber,
                        UnitNumber = contract.Unit?.UnitNumber
                    });
                    currentMonth = currentMonth.AddMonths(1);
                    monthCounter++;
                }
            }

            // 1.b) إضافة المصروفات المحملة على حساب المستأجر (Debit)
            var chargedExpenses = await _db.Expenses
                .Include(e => e.Unit)
                .Where(e => e.TenantId == tenantId
                         && e.IsChargedToTenant
                         && e.IsActive
                         && e.ExpenseDate >= fromDate
                         && e.ExpenseDate <= toDate)
                .ToListAsync();

            foreach (var exp in chargedExpenses)
            {
                transactions.Add(new AccountTransactionDto
                {
                    Id = exp.Id * 150000,
                    ReferenceNumber = exp.ExpenseNumber,
                    TransactionDate = exp.ExpenseDate,
                    TransactionType = "Debit",
                    Category = "Expense",
                    CategoryLabel = GetExpenseTypeLabel(exp.ExpenseType),
                    Description = $"مصروف محمّل: {exp.Description}{(exp.PaidTo != null ? $" — مدفوع لـ {exp.PaidTo}" : "")}",
                    Debit = exp.Amount,
                    Credit = 0,
                    ContractId = null,
                    ContractNumber = null,
                    UnitNumber = exp.Unit?.UnitNumber
                });
            }

            // 2.a) إضافة الدفعات (Credit)
            foreach (var payment in payments)
            {
                transactions.Add(new AccountTransactionDto
                {
                    Id = payment.Id,
                    ReferenceNumber = payment.ReceiptNumber,
                    TransactionDate = payment.PaymentDate,
                    TransactionType = "Credit",
                    Category = payment.PaymentType.ToString(),
                    CategoryLabel = GetPaymentTypeLabel(payment.PaymentType),
                    Description = $"دفعة {GetPaymentTypeLabel(payment.PaymentType)} — {payment.Notes ?? "بدون ملاحظات"}",
                    Debit = 0,
                    Credit = payment.Amount,
                    ContractId = payment.ContractId,
                    ContractNumber = payment.Contract?.ContractNumber,
                    UnitNumber = payment.Contract?.Unit?.UnitNumber,
                    PaymentMethod = GetPaymentMethodLabel(payment.PaymentMethod),
                    Notes = payment.Notes
                });
            }

            // ===== 2. ترتيب الحركات وحساب الرصيد الجاري =====
            transactions = transactions.OrderBy(t => t.TransactionDate).ThenBy(t => t.TransactionType == "Debit" ? 0 : 1).ToList();

            decimal runningBalance = 0;
            foreach (var trans in transactions)
            {
                runningBalance += trans.Debit - trans.Credit;
                trans.RunningBalance = runningBalance;
            }

            // ===== 3. حساب الإجماليات =====
            decimal totalDebit = transactions.Sum(t => t.Debit);
            decimal totalCredit = transactions.Sum(t => t.Credit);
            decimal currentBalance = totalDebit - totalCredit;

            string balanceStatus = currentBalance > 0 ? "Debtor" : (currentBalance < 0 ? "Creditor" : "Settled");

            // ===== 4. ملخصات العقود =====
            var contractSummaries = new List<ContractSummaryDto>();
            foreach (var c in contracts)
            {
                var contractDue = transactions.Where(t => t.ContractId == c.Id && t.TransactionType == "Debit").Sum(t => t.Debit);
                var contractPaid = transactions.Where(t => t.ContractId == c.Id && t.TransactionType == "Credit").Sum(t => t.Credit);

                contractSummaries.Add(new ContractSummaryDto
                {
                    ContractId = c.Id,
                    ContractNumber = c.ContractNumber,
                    UnitNumber = c.Unit?.UnitNumber ?? "",
                    UnitName = c.Unit?.UnitName ?? "",
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    MonthlyRent = c.RentAmount,
                    DepositAmount = c.DepositAmount,
                    Status = c.Status.ToString(),
                    ContractDue = contractDue,
                    ContractPaid = contractPaid,
                    ContractBalance = contractDue - contractPaid
                });
            }

            // ===== 5. تحليلات مخصصة =====
            var lastPayment = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
            var avgPayment = payments.Any() ? payments.Average(p => p.Amount) : 0;

            int latePayments = 0;
            foreach (var contract in contracts.Where(c => c.Status == ContractStatus.Active))
            {
                var contractPayments = payments.Where(p => p.ContractId == contract.Id).OrderBy(p => p.PaymentDate).ToList();
                var expectedDate = contract.StartDate;
                int monthsChecked = 0;
                while (expectedDate <= today && monthsChecked < 120)
                {
                    var paidInMonth = contractPayments.FirstOrDefault(p => p.PaymentDate >= expectedDate && p.PaymentDate <= expectedDate.AddDays(30));
                    if (paidInMonth == null) latePayments++;
                    expectedDate = expectedDate.AddMonths(1);
                    monthsChecked++;
                }
            }

            // ===== 6. تفصيل شهري =====
            var monthlyBreakdown = transactions
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
                .Take(12)
                .Select(g => new MonthlyAccountBreakdownDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new CultureInfo("ar-LY").DateTimeFormat.GetMonthName(g.Key.Month),
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit),
                    NetBalance = g.Sum(x => x.Debit) - g.Sum(x => x.Credit)
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            // ===== 7. تجميع المبالغ بالـ Enums الدقيقة =====
            var totalDeposits = payments.Where(p => p.PaymentType == PaymentType.Deposit).Sum(p => p.Amount);
            var totalRent = payments.Where(p => p.PaymentType == PaymentType.Rent).Sum(p => p.Amount);
            var totalFees = payments.Where(p => p.PaymentType == PaymentType.Fees).Sum(p => p.Amount);
            var totalUtilities = payments.Where(p => p.PaymentType == PaymentType.Electricity || p.PaymentType == PaymentType.Water).Sum(p => p.Amount);

            var totalChargedExpenses = chargedExpenses.Sum(e => e.Amount);

            DateTime? nextExpected = null;
            var activeContract = contracts.FirstOrDefault(c => c.Status == ContractStatus.Active);
            if (activeContract != null && lastPayment != null)
            {
                nextExpected = lastPayment.PaymentDate.AddMonths(1);
            }

            return new TenantAccountStatementDto
            {
                TenantId = tenant.Id,
                TenantName = tenant.FullName,
                TenantPhone = tenant.Phone,
                NationalId = tenant.NationalId,
                ContactPerson = tenant.ContactPerson,
                CreatedAt = tenant.CreatedAt,
                Contracts = contractSummaries,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                CurrentBalance = currentBalance,
                BalanceStatus = balanceStatus,
                TotalDeposits = totalDeposits,
                TotalRentDue = totalRent,
                TotalPenalties = totalFees + totalChargedExpenses,
                TotalUtilities = totalUtilities,
                AveragePaymentAmount = avgPayment,
                LastPaymentDate = lastPayment?.PaymentDate,
                NextExpectedPaymentDate = nextExpected,
                LatePaymentsCount = latePayments,
                Transactions = transactions.OrderByDescending(t => t.TransactionDate).ToList(),
                MonthlyBreakdown = monthlyBreakdown
            };
        }

        // ===== دالة مساعدة لترجمة نوع المصروف (مطابقة للـ Enum الفعلي) =====
        private static string GetExpenseTypeLabel(ExpenseType type) => type switch
        {
            ExpenseType.Maintenance => "صيانة وإصلاحات",
            ExpenseType.Utilities => "فواتير عامة (كهرباء/مياه)",
            ExpenseType.Security => "حراسة وأمن",
            ExpenseType.Cleaning => "نظافة عامة",
            ExpenseType.Management => "مصاريف إدارية",
            ExpenseType.Other => "أخرى",
            _ => "غير محدد"
        };

        // =====================================================
        // نظرة شاملة على أرصدة جميع المستأجرين
        // =====================================================
        public async Task<List<TenantBalanceOverviewDto>> GetAllTenantsBalancesAsync()
        {
            var tenants = await _db.Tenants
                .Where(t => t.IsActive)
                .ToListAsync();

            var result = new List<TenantBalanceOverviewDto>();
            var today = DateTime.Today;

            foreach (var tenant in tenants)
            {
                var contracts = await _db.Contracts
                    .Where(c => c.TenantId == tenant.Id && c.IsActive)
                    .ToListAsync();

                var payments = await _db.Payments
                    .Where(p => p.TenantId == tenant.Id && p.IsActive)
                    .ToListAsync();

                // 1. حساب مدين العقود (إيجار + تأمين)
                decimal totalDebit = 0;
                foreach (var contract in contracts.Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired))
                {
                    var endDate = contract.EndDate > today ? today : contract.EndDate;
                    int months = (int)((endDate - contract.StartDate).TotalDays / 30);
                    if (months < 1) months = 1;
                    totalDebit += months * contract.RentAmount;
                    totalDebit += contract.DepositAmount;
                }

                // 2. 👈 الجديد: حساب المصروفات المحملة على المستأجر وإضافتها للـ Debit
                decimal totalChargedExpenses = await _db.Expenses
                    .Where(e => e.TenantId == tenant.Id && e.IsChargedToTenant && e.IsActive)
                    .SumAsync(e => e.Amount);

                totalDebit += totalChargedExpenses;

                // 3. حساب الدائن (المدفوعات)
                decimal totalCredit = payments.Sum(p => p.Amount);

                // 4. الرصيد الحالي
                decimal balance = totalDebit - totalCredit;

                string status = balance > 0 ? "Debtor" : (balance < 0 ? "Creditor" : "Settled");

                result.Add(new TenantBalanceOverviewDto
                {
                    TenantId = tenant.Id,
                    TenantName = tenant.FullName,
                    TenantPhone = tenant.Phone,
                    ContractsCount = contracts.Count,
                    ActiveContractsCount = contracts.Count(c => c.Status == ContractStatus.Active),
                    TotalDebit = totalDebit,
                    TotalCredit = totalCredit,
                    Balance = balance,
                    BalanceStatus = status,
                    LastPaymentDate = payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate,
                    TransactionsCount = payments.Count + (totalChargedExpenses > 0 ? 1 : 0) // حساب حركة المصروفات
                });
            }

            return result.OrderByDescending(r => Math.Abs(r.Balance)).ToList();
        }
        private static string GetPaymentTypeLabel(PaymentType type) => type switch
        {
            PaymentType.Rent => "إيجار شهري",
            PaymentType.Electricity => "فاتورة كهرباء",
            PaymentType.Water => "فاتورة مياه",
            PaymentType.Fees => "رسوم إضافية / غرامة",
            PaymentType.Deposit => "عربون / ضمان العقد",
            PaymentType.Maintenance => "مصاريف صيانة",
            PaymentType.Other => "أخرى",
            _ => "غير محدد"
        };

        // ✅ تصحيح: ترجمة طريقة الدفع حسب الـ Enum الخاصة بك
        private static string GetPaymentMethodLabel(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "نقداً",
            PaymentMethod.Transfer => "حوالة بنكية / صك",
            PaymentMethod.Check => "شيك مصرفي",
            PaymentMethod.Card => "بطاقة سداد إلكترونية",
            _ => "غير محدد"
        };
    }
}