using Andalos.API.DTOs.Tenants;

namespace Andalos.API.Interfaces
{
    public interface ITenantService
    {
        Task<List<TenantResponseDto>> GetAllAsync();
        Task<TenantResponseDto?> GetByIdAsync(int id);
        Task<TenantResponseDto> CreateAsync(CreateTenantDto dto);
        Task<TenantResponseDto?> UpdateAsync(int id, UpdateTenantDto dto);
        Task<bool> DeleteAsync(int id);
    }
}