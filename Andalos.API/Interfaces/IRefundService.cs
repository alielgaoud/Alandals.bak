using Andalos.API.DTOs.Refunds;

namespace Andalos.API.Interfaces
{
    public interface IRefundService
    {
        Task<List<RefundResponseDto>> GetAllAsync();
        Task<List<RefundResponseDto>> GetByContractAsync(int contractId);
        Task<RefundResponseDto> CreateAsync(CreateRefundDto dto);
        Task<bool> DeleteAsync(int id); // حذف منطقي (إلغاء المرتجع)
    }
}