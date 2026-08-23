using Andalos.API.DTOs.Payments;

namespace Andalos.API.Interfaces
{
    public interface IPaymentService
    {
        Task<List<PaymentResponseDto>> GetAllAsync();
        Task<List<PaymentResponseDto>> GetByContractAsync(int contractId);
        Task<List<PaymentResponseDto>> GetByTenantAsync(int tenantId);
        Task<PaymentResponseDto> CreateAsync(CreatePaymentDto dto);
        Task<bool> DeleteAsync(int id);
        Task<PaymentSummaryDto> GetContractSummaryAsync(int contractId);
        Task<List<PaymentSummaryDto>> GetAllSummariesAsync();
    }
}