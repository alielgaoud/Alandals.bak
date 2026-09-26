using Andalos.API.DTOs.Contracts;
using Andalos.API.DTOs.Payments;
using Andalos.API.Enums;

namespace Andalos.API.Interfaces
{
    public interface IContractService
    {
        Task<List<ContractResponseDto>> GetAllAsync();
        Task<ContractResponseDto?> GetByIdAsync(int id);
        Task<ContractResponseDto> CreateAsync(CreateContractDto dto);
        Task<ContractResponseDto> UpdateAsync(int id, UpdateContractDto dto);
        Task<bool> UpdateStatusAsync(int id, ContractStatus newStatus);
        Task<bool> DeleteAsync(int id);
        Task<ContractResponseDto> RenewAsync(int contractId, RenewContractDto dto);

        // Endpoints إضافية لزيادة الترابط
        Task<List<ContractResponseDto>> GetByTenantAsync(int tenantId);
        Task<List<ContractResponseDto>> GetByUnitAsync(int unitId);
        Task<List<ContractResponseDto>> GetExpiringAsync(int days);
        Task<ContractRenewalChainDto?> GetRenewalChainAsync(int contractId);
        Task<ContractFinancialSummaryDto?> GetFinancialSummaryAsync(int contractId);

        // مجموعة مالية جديدة
        Task<List<PaymentResponseDto>> GetPaymentsAsync(int contractId);
        Task<ContractStatementDto?> GetStatementAsync(int contractId, DateTime? fromDate, DateTime? toDate);
        Task<ContractFeeResponseDto> AddFeeAsync(int contractId, CreateContractFeeDto dto);
        Task<ContractFeeResponseDto> UpdateFeeAsync(int contractId, int feeId, UpdateContractFeeDto dto);
        Task<bool> DeleteFeeAsync(int contractId, int feeId);
        Task<PaymentResponseDto?> ProcessDueAsync(int contractId);
    }

    public class ContractRenewalChainDto
    {
        public int RootContractId { get; set; }
        public List<ContractResponseDto> Chain { get; set; } = new();
    }

    public class ContractFinancialSummaryDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public decimal RentAmount { get; set; }
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Remaining { get; set; }
        public decimal TotalFeesOneTime { get; set; }
        public decimal TotalFeesMonthly { get; set; }
        public decimal DepositAmount { get; set; }
        public int PaymentsCount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public List<ContractFeeResponseDto> Fees { get; set; } = new();
    }

    public class ContractStatementDto
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string TenantName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal RentAmount { get; set; }
        public decimal DepositAmount { get; set; }
        public List<ContractFeeResponseDto> Fees { get; set; } = new();
        public List<PaymentResponseDto> Payments { get; set; } = new();
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Remaining { get; set; }
        public decimal TotalFeesCalculated { get; set; }
        public List<ContractMonthlyDueDto> MonthlyBreakdown { get; set; } = new();
    }

    public class ContractMonthlyDueDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal RentDue { get; set; }
        public decimal FeesDue { get; set; }
        public decimal TotalDue { get; set; }
        public decimal Paid { get; set; }
        public decimal Balance { get; set; }
        public DateTime DueDate { get; set; }
    }
}