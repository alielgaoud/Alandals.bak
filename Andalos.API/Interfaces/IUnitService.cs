using Andalos.API.DTOs.Units;

namespace Andalos.API.Interfaces
{
    public interface IUnitService
    {
        Task<List<UnitResponseDto>> GetAllAsync();
        Task<UnitResponseDto?> GetByIdAsync(int id);
        Task<UnitResponseDto> CreateAsync(CreateUnitDto dto);
        Task<UnitResponseDto?> UpdateAsync(int id, UpdateUnitDto dto);
        Task<bool> DeleteAsync(int id);
        Task<int> GetCountByStatusAsync(string status);
    }
}