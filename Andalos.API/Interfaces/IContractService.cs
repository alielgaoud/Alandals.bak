using Andalos.API.DTOs.Contracts;
using Andalos.API.Enums;

namespace Andalos.API.Interfaces
{
    public interface IContractService
    {
        Task<List<ContractResponseDto>> GetAllAsync();
        Task<ContractResponseDto?> GetByIdAsync(int id);
        Task<ContractResponseDto> CreateAsync(CreateContractDto dto);
        Task<bool> UpdateStatusAsync(int id, ContractStatus newStatus);
        Task<bool> DeleteAsync(int id);
    }
}