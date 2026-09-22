using Andalos.API.DTOs.Circulars;

namespace Andalos.API.Interfaces
{
    public interface ICircularService
    {
        // ===== للإدارة =====
        Task<CircularResponseDto> CreateAsync(CreateCircularDto dto, string createdBy);
        Task<CircularResponseDto> UpdateAsync(int id, UpdateCircularDto dto, string updatedBy);
        Task<bool> DeleteAsync(int id);
        Task<List<CircularResponseDto>> GetAllAsync();
        Task<CircularResponseDto?> GetByIdAsync(int id);

        // ===== لبوابة المستأجر =====
        Task<List<TenantCircularDto>> GetMyCircularsAsync();

        // ===== للمجدول =====
        Task<int> PublishDueScheduledCircularsAsync();
    }
}