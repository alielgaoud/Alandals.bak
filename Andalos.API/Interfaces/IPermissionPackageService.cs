using Andalos.API.DTOs.Users;

namespace Andalos.API.Interfaces
{
    public interface IPermissionPackageService
    {
        Task<List<PermissionModuleDto>> GetModulesAsync();
        Task<List<PermissionPackageResponseDto>> GetAllAsync();
        Task<PermissionPackageResponseDto?> GetByIdAsync(int id);
        Task<PermissionPackageResponseDto> CreateAsync(CreatePermissionPackageDto dto);
        Task<PermissionPackageResponseDto?> UpdateAsync(int id, UpdatePermissionPackageDto dto);
        Task<bool> DeleteAsync(int id);

        Task<bool> AssignPackagesToUserAsync(AssignPackagesToUserDto dto);
        Task<List<PermissionPackageResponseDto>> GetUserPackagesAsync(int userId);
        Task<List<string>> GetEffectivePermissionsForUserAsync(int userId);
    }
}