namespace Andalos.API.DTOs.Tenants
{
    // ===== كشف حساب المستأجر الشامل =====
    public class TenantAccountStatementDto
    {
        // معلومات المستأجر
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public DateTime CreatedAt { get; set; }

        // معلومات العقود
        public List<ContractSummaryDto> Contracts { get; set; } = new();

        // ملخص الحساب
        public decimal TotalDebit { get; set; }       // إجمالي المدين (المستحقات)
        public decimal TotalCredit { get; set; }      // إجمالي الدائن (المدفوعات)
        public decimal CurrentBalance { get; set; }   // الرصيد الحالي (سالب = مدين، موجب = دائن)
        public string BalanceStatus { get; set; } = string.Empty; // "Creditor" أو "Debtor" أو "Settled"

        // تحليلات إضافية
        public decimal TotalDeposits { get; set; }    // إجمالي التأمينات
        public decimal TotalRentDue { get; set; }     // إجمالي الإيجار المستحق
        public decimal TotalPenalties { get; set; }   // إجمالي الغرامات
        public decimal TotalUtilities { get; set; }   // إجمالي فواتير الخدمات
        public decimal AveragePaymentAmount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public DateTime? NextExpectedPaymentDate { get; set; }
        public int LatePaymentsCount { get; set; }    // عدد التأخيرات

        // الحركات (Transactions) - القلب النابض للنظام
        public List<AccountTransactionDto> Transactions { get; set; } = new();

        // تحليل شهري للـ 12 شهر الأخيرة
        public List<MonthlyAccountBreakdownDto> MonthlyBreakdown { get; set; } = new();
    }

    // ===== حركة مالية واحدة (Transaction) =====
    public class AccountTransactionDto
    {
        public int Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty; // رقم السند/الإيصال
        public DateTime TransactionDate { get; set; }
        public string TransactionType { get; set; } = string.Empty; // "Debit" أو "Credit"
        public string Category { get; set; } = string.Empty;        // Rent, Deposit, Maintenance, Payment...
        public string CategoryLabel { get; set; } = string.Empty;   // بالعربي
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }              // المدين (مستحق على المستأجر)
        public decimal Credit { get; set; }             // الدائن (مدفوع من المستأجر)
        public decimal RunningBalance { get; set; }     // الرصيد الجاري بعد الحركة
        public int? ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public string? UnitNumber { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
    }

    // ===== ملخص العقد داخل الكشف =====
    public class ContractSummaryDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyRent { get; set; }
        public decimal DepositAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal ContractDue { get; set; }
        public decimal ContractPaid { get; set; }
        public decimal ContractBalance { get; set; }
    }

    // ===== تفصيل شهري =====
    public class MonthlyAccountBreakdownDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal NetBalance { get; set; }
    }

    // ===== كشف مختصر لكل المستأجرين =====
    public class TenantBalanceOverviewDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantPhone { get; set; } = string.Empty;
        public int ContractsCount { get; set; }
        public int ActiveContractsCount { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }
        public string BalanceStatus { get; set; } = string.Empty; // "Creditor" | "Debtor" | "Settled"
        public DateTime? LastPaymentDate { get; set; }
        public int TransactionsCount { get; set; }
    }
}