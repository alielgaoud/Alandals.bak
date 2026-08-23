using Andalos.API.DTOs.Maintenance;

namespace Andalos.API.Interfaces
{
    public interface IMaintenanceService
    {
        Task<List<MaintenanceResponseDto>> GetAllAsync();
        Task<List<MaintenanceResponseDto>> GetByUnitAsync(int unitId);
        Task<MaintenanceResponseDto?> GetByIdAsync(int id);
        Task<MaintenanceResponseDto> CreateAsync(CreateMaintenanceRequestDto dto);
        Task<bool> UpdateStatusAsync(int id, UpdateMaintenanceStatusDto dto);
        Task<bool> DeleteAsync(int id);
    }
}