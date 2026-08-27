using Andalos.API.DTOs.Blacklist;

namespace Andalos.API.Interfaces
{
    public interface IVisitorBlacklistService
    {
        Task<List<BlacklistResponseDto>> GetAllAsync();
        Task<BlacklistResponseDto?> GetByIdAsync(int id);
        Task<BlacklistResponseDto> AddAsync(CreateBlacklistDto dto, string createdBy);
        Task<bool> RemoveAsync(int id); // رفع الحظر
        Task<CheckBlacklistResultDto> CheckVisitorAsync(string? phone, string? nationalId = null, string? fullName = null);
    }
}