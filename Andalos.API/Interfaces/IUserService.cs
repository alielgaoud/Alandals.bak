using Andalos.API.DTOs.Users;

namespace Andalos.API.Interfaces
{
    public interface IUserService
    {
        Task<List<UserResponseDto>> GetAllAsync();
        Task<UserResponseDto?> GetByIdAsync(int id);
        Task<UserResponseDto> CreateUserAsync(CreateUserByAdminDto dto);
        Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto dto);
        Task<bool> ResetPasswordAsync(int id, string newPassword);
        Task<bool> ToggleLockAccountAsync(int id, bool lockAccount);
        Task<bool> DeleteUserAsync(int id);
    }
}