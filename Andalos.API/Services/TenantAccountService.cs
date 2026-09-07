using Andalos.API.Data;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Andalos.API.Services
{
    public interface ITenantAccountService
    {
        Task<TenantAccountStatementDto?> GetStatementAsync(int tenantId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<TenantBalanceOverviewDto>> GetAllTenantsBalancesAsync();

        Task<Payment> DepositAdvancePaymentAsync(int tenantId, decimal amount, PaymentMethod method, string notes);
        Task ProcessMonthlyRentDuesAsync();
    }

    public class TenantAccountService : ITenantAccountService
    {
        private readonly AppDbContext _db;
        private readonly INumberGeneratorService _numberGenerator;

        public TenantAccountService(AppDbContext db, INumberGeneratorService numberGenerator)
        {
            _db = db;
            _numberGenerator = numberGenerator;
        }

        // =====================================================
        // 1. إيداع دفعة مقدمة في حساب المستأجر (Wallet)
        // =====================================================
        public async Task<Payment> DepositAdvancePaymentAsync(int tenantId, decimal amount, PaymentMethod method, string notes)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("المستأجر غير موجود");

            var activeContract = await _db.Contracts
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Status == ContractStatus.Active && c.IsActive);

            if (activeContract == null)
            {
                activeContract = await _db.Contracts
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.IsActive);
            }

            if (activeContract == null)
                throw new InvalidOperationException("لا يوجد عقد مسجل للمستأجر لربط سند الإيداع به");

            tenant.CreditBalance += amount;

            string receiptNo = await _numberGenerator.GenerateReceiptNumberAsync();

            var payment = new Payment
            {
                TenantId = tenantId,
                ContractId = activeContract.Id,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentType = PaymentType.AdvancePayment,
                PaymentMethod = method,
                ReceiptNumber = receiptNo,
                Notes = string.IsNullOrWhiteSpace(notes) ? "إيداع دفعة مقدمة في رصيد المستأجر" : notes,
                IsActive = true
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return payment;
        }

        // =====================================================
        // 2. المعالجة الشهرية: الخصم التلقائي
        // =====================================================
        public async Task ProcessMonthlyRentDuesAsync()
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var activeContracts = await _db.Contracts
                .Include(c => c.Tenant)
                .Include(c => c.ContractFees)
                .Where(c => c.IsActive && c.Status == ContractStatus.Active && c.Tenant!.CreditBalance > 0)
                .ToListAsync();

            foreach (var contract in activeContracts)
            {
                bool alreadyProcessedThisMonth = await _db.Payments.AnyAsync(p =>
                    p.ContractId == contract.Id &&
                    p.PaymentType == PaymentType.Rent &&
                    p.PaymentDate >= startOfMonth &&
                    p.PaymentDate <= endOfMonth &&
                    p.IsActive);

                if (alreadyProcessedThisMonth) continue;

                var tenant = contract.Tenant;
                int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
                decimal totalContractValue = contract.RentAmount * durationMonths;

                decimal monthlyFeesTotal = contract.ContractFees
                    .Where(f => f.Frequency == FeeFrequency.Monthly)
                    .Sum(f => f.CalculateActualAmount(contract.RentAmount, totalContractValue));

                decimal totalMonthlyDue = contract.RentAmount + monthlyFeesTotal;

                if (tenant!.CreditBalance >= totalMonthlyDue)
                {
                    tenant.CreditBalance -= totalMonthlyDue;

                    string receiptNo = await _numberGenerator.GenerateReceiptNumberAsync();
                    var rentPayment = new Payment
                    {
                        TenantId = tenant.Id,
                        ContractId = contract.Id,
                        Amount = totalMonthlyDue,
                        PaymentDate = today,
                        PaymentType = PaymentType.Rent,
                        PaymentMethod = PaymentMethod.FromBalance,
                        ReceiptNumber = receiptNo,
                        Notes = $"خصم آلي لإيجار شهر {today:MM/yyyy} شامل الرسوم الشهرية ({monthlyFeesTotal} د.ل)",
                        IsActive = true
                    };
                    _db.Payments.Add(rentPayment);
                }
                else if (tenant.CreditBalance > 0)
                {
                    decimal partialAmount = tenant.CreditBalance;
                    tenant.CreditBalance = 0;

                    string receiptNo = await _numberGenerator.GenerateReceiptNumberAsync();
                    var partialPayment = new Payment
                    {
                        TenantId = tenant.Id,
                        ContractId = contract.Id,
                        Amount = partialAmount,
                        PaymentDate = today,
                        PaymentType = PaymentType.Rent,
                        PaymentMethod = PaymentMethod.FromBalance,
                        ReceiptNumber = receiptNo,
                        Notes = $"خصم جزئي لإيجار شهر {today:MM/yyyy} شامل الرسوم من المتبقي بالرصيد",
                        IsActive = true
                    };
                    _db.Payments.Add(partialPayment);
                }
            }

            await _db.SaveChangesAsync();
        }

        // =====================================================
        // 3. كشف حساب شامل لمستأجر واحد (مُصحح مع فلتر التاريخ)
        // =====================================================
        public async Task<TenantAccountStatementDto?> GetStatementAsync(int tenantId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null) return null;

            var today = DateTime.Today;
            fromDate ??= new DateTime(today.Year - 2, 1, 1);

            // 👈 إصلاح جوهري: ضبط تاريخ النهاية ليشمل كامل اليوم الحالي حتى الساعة 23:59:59
            var actualToDate = (toDate ?? today).Date.AddDays(1).AddTicks(-1);

            var contracts = await _db.Contracts
                .Include(c => c.Unit)
                .Include(c => c.ContractFees)
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync();

            var payments = await _db.Payments
                .Include(p => p.Contract).ThenInclude(c => c!.Unit)
                .Where(p => p.TenantId == tenantId && p.IsActive
                    && p.PaymentDate >= fromDate && p.PaymentDate <= actualToDate) // 👈 تم الاستبدال لـ actualToDate
                .ToListAsync();

            var transactions = new List<AccountTransactionDto>();

            // --- 1. المستحقات (Debit) ---
            foreach (var contract in contracts.Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired || c.Status == ContractStatus.Renewed))
            {
                var contractStart = contract.StartDate < fromDate.Value ? fromDate.Value : contract.StartDate;
                var contractEnd = contract.EndDate > actualToDate ? actualToDate : contract.EndDate;
                if (contractEnd > today) contractEnd = today;

                int durationMonths = Math.Max(1, (int)((contract.EndDate - contract.StartDate).TotalDays / 30));
                decimal totalContractValue = contract.RentAmount * durationMonths;

                // أ) قيد التأمين / العربون عند بداية العقد
                if (contract.DepositAmount > 0 && contract.StartDate >= fromDate && contract.StartDate <= actualToDate)
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

                // ب) الرسوم والعمولات التي تُدفع مَرّة واحدة
                var oneTimeFees = contract.ContractFees.Where(f => f.Frequency == FeeFrequency.OneTime);
                foreach (var fee in oneTimeFees)
                {
                    if (contract.StartDate >= fromDate && contract.StartDate <= actualToDate)
                    {
                        decimal feeAmount = fee.CalculateActualAmount(contract.RentAmount, totalContractValue);
                        transactions.Add(new AccountTransactionDto
                        {
                            Id = -contract.Id * 20000 - fee.Id,
                            ReferenceNumber = $"FEE-{contract.ContractNumber}-{fee.Id}",
                            TransactionDate = contract.StartDate,
                            TransactionType = "Debit",
                            Category = "Fees",
                            CategoryLabel = "رسوم / عمولة عقد",
                            Description = $"{fee.FeeName} — عقد {contract.ContractNumber}",
                            Debit = feeAmount,
                            Credit = 0,
                            ContractId = contract.Id,
                            ContractNumber = contract.ContractNumber,
                            UnitNumber = contract.Unit?.UnitNumber
                        });
                    }
                }

                // ج) قيود الإيجار الشهرية + الرسوم الشهرية المتكررة
                var monthlyFees = contract.ContractFees.Where(f => f.Frequency == FeeFrequency.Monthly).ToList();
                decimal monthlyFeesAmount = monthlyFees.Sum(f => f.CalculateActualAmount(contract.RentAmount, totalContractValue));

                var currentMonth = new DateTime(contractStart.Year, contractStart.Month, contractStart.Day);
                int monthCounter = 0;
                while (currentMonth <= contractEnd && monthCounter < 240)
                {
                    decimal totalMonthlyDebit = contract.RentAmount + monthlyFeesAmount;
                    string feesNote = monthlyFeesAmount > 0 ? $" (تتضمن رسوم شهرية {monthlyFeesAmount} د.ل)" : "";

                    transactions.Add(new AccountTransactionDto
                    {
                        Id = -contract.Id * 10000 - monthCounter,
                        ReferenceNumber = $"RENT-{contract.ContractNumber}-{currentMonth:yyyyMM}",
                        TransactionDate = currentMonth,
                        TransactionType = "Debit",
                        Category = "Rent",
                        CategoryLabel = "إيجار شهري ورسوم",
                        Description = $"إيجار شهر {currentMonth:MM/yyyy}{feesNote} — {contract.TradeName ?? contract.Unit?.UnitNumber ?? ""}",
                        Debit = totalMonthlyDebit,
                        Credit = 0,
                        ContractId = contract.Id,
                        ContractNumber = contract.ContractNumber,
                        UnitNumber = contract.Unit?.UnitNumber
                    });

                    currentMonth = currentMonth.AddMonths(1);
                    monthCounter++;
                }
            }

            // --- 2. المصروفات المحملة على المستأجر (Debit) ---
            var chargedExpenses = await _db.Expenses
                .Include(e => e.Unit)
                .Where(e => e.TenantId == tenantId && e.IsChargedToTenant && e.IsActive
                         && e.ExpenseDate >= fromDate && e.ExpenseDate <= actualToDate)
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
                    Description = $"مصروف محمّل: {exp.Description}",
                    Debit = exp.Amount,
                    Credit = 0,
                    ContractId = null,
                    ContractNumber = null,
                    UnitNumber = exp.Unit?.UnitNumber
                });
            }

            // --- 3. المدفوعات (Credit) ---
            foreach (var payment in payments)
            {
                decimal actualCredit = payment.PaymentMethod == PaymentMethod.FromBalance ? 0 : payment.Amount;

                transactions.Add(new AccountTransactionDto
                {
                    Id = payment.Id,
                    ReferenceNumber = payment.ReceiptNumber,
                    TransactionDate = payment.PaymentDate,
                    TransactionType = actualCredit > 0 ? "Credit" : "Transfer",
                    Category = payment.PaymentType.ToString(),
                    CategoryLabel = GetPaymentTypeLabel(payment.PaymentType),
                    Description = payment.PaymentMethod == PaymentMethod.FromBalance
                        ? $"[تسوية من الرصيد] {payment.Notes}"
                        : $"دفعة {GetPaymentTypeLabel(payment.PaymentType)} — {payment.Notes ?? "بدون ملاحظات"}",
                    Debit = 0,
                    Credit = actualCredit,
                    ContractId = payment.ContractId,
                    ContractNumber = payment.Contract?.ContractNumber,
                    UnitNumber = payment.Contract?.Unit?.UnitNumber,
                    PaymentMethod = GetPaymentMethodLabel(payment.PaymentMethod),
                    Notes = payment.Notes
                });
            }

            // --- 4. ترتيب الحركات وحساب الرصيد الجاري ---
            transactions = transactions.OrderBy(t => t.TransactionDate).ThenBy(t => t.TransactionType == "Debit" ? 0 : 1).ToList();

            decimal runningBalance = 0;
            foreach (var trans in transactions)
            {
                runningBalance += trans.Debit - trans.Credit;
                trans.RunningBalance = runningBalance;
            }

            decimal totalDebit = transactions.Sum(t => t.Debit);
            decimal totalCredit = transactions.Sum(t => t.Credit);
            decimal currentBalance = totalDebit - totalCredit;

            string balanceStatus = currentBalance > 0 ? "Debtor" : (currentBalance < 0 ? "Creditor" : "Settled");

            // --- 5. ملخصات العقود ---
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
                    UnitName = c.TradeName ?? c.Unit?.UnitNumber ?? "",
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

            var lastPayment = payments.Where(p => p.PaymentMethod != PaymentMethod.FromBalance).OrderByDescending(p => p.PaymentDate).FirstOrDefault();
            var avgPayment = payments.Any(p => p.PaymentMethod != PaymentMethod.FromBalance)
                             ? payments.Where(p => p.PaymentMethod != PaymentMethod.FromBalance).Average(p => p.Amount)
                             : 0;

            int latePayments = 0;

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
                }).OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();

            var totalDeposits = payments.Where(p => p.PaymentType == PaymentType.Deposit || p.PaymentType == PaymentType.AdvancePayment).Sum(p => p.Amount);
            var totalRent = payments.Where(p => p.PaymentType == PaymentType.Rent).Sum(p => p.Amount);
            var totalFees = payments.Where(p => p.PaymentType == PaymentType.Fees).Sum(p => p.Amount);
            var totalUtilities = payments.Where(p => p.PaymentType == PaymentType.Electricity || p.PaymentType == PaymentType.Water).Sum(p => p.Amount);
            var totalChargedExpenses = chargedExpenses.Sum(e => e.Amount);

            DateTime? nextExpected = contracts.FirstOrDefault(c => c.Status == ContractStatus.Active) != null
                                     && lastPayment != null ? lastPayment.PaymentDate.AddMonths(1) : null;

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

        public async Task<List<TenantBalanceOverviewDto>> GetAllTenantsBalancesAsync()
        {
            var tenants = await _db.Tenants.Where(t => t.IsActive).ToListAsync();
            var result = new List<TenantBalanceOverviewDto>();
            var today = DateTime.Today;

            foreach (var tenant in tenants)
            {
                var contracts = await _db.Contracts.Where(c => c.TenantId == tenant.Id && c.IsActive).ToListAsync();
                var payments = await _db.Payments.Where(p => p.TenantId == tenant.Id && p.IsActive).ToListAsync();

                decimal totalDebit = 0;
                foreach (var contract in contracts.Where(c => c.Status == ContractStatus.Active || c.Status == ContractStatus.Expired))
                {
                    var endDate = contract.EndDate > today ? today : contract.EndDate;
                    int months = (int)((endDate - contract.StartDate).TotalDays / 30);
                    if (months < 1) months = 1;
                    totalDebit += months * contract.RentAmount;
                    totalDebit += contract.DepositAmount;
                }

                decimal totalChargedExpenses = await _db.Expenses
                    .Where(e => e.TenantId == tenant.Id && e.IsChargedToTenant && e.IsActive)
                    .SumAsync(e => e.Amount);

                totalDebit += totalChargedExpenses;

                decimal totalCredit = payments.Where(p => p.PaymentMethod != PaymentMethod.FromBalance).Sum(p => p.Amount);

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
                    TransactionsCount = payments.Count + (totalChargedExpenses > 0 ? 1 : 0)
                });
            }

            return result.OrderByDescending(r => Math.Abs(r.Balance)).ToList();
        }

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

        private static string GetPaymentTypeLabel(PaymentType type) => type switch
        {
            PaymentType.Rent => "إيجار شهري",
            PaymentType.Electricity => "فاتورة كهرباء",
            PaymentType.Water => "فاتورة مياه",
            PaymentType.Fees => "رسوم إضافية / غرامة",
            PaymentType.Deposit => "عربون / ضمان العقد",
            PaymentType.Maintenance => "مصاريف صيانة",
            PaymentType.AdvancePayment => "إيداع دفعة مقدمة",
            PaymentType.Other => "أخرى",
            _ => "غير محدد"
        };

        private static string GetPaymentMethodLabel(PaymentMethod method) => method switch
        {
            PaymentMethod.Cash => "نقداً",
            PaymentMethod.Transfer => "حوالة بنكية / صك",
            PaymentMethod.Check => "شيك مصرفي",
            PaymentMethod.Card => "بطاقة سداد إلكترونية",
            PaymentMethod.FromBalance => "خصم آلي من الرصيد",
            _ => "غير محدد"
        };
    }
}