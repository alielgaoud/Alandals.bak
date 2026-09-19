using Andalos.API.DTOs.System;
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
        Task<List<UserResponseDto>> GetUsersByTenantIdAsync(int tenantId);
        Task<UserPermissionsResponseDto?> GetUserPermissionsAsync(int userId);
        Task<bool> AssignPermissionsAsync(AssignUserPermissionsDto dto);
        // 👈 جديد: جلب سجل التدقيق والمراقبة (Audit Logs)
        Task<List<AuditLogDto>> GetAuditLogsAsync(DateTime? fromDate, DateTime? toDate, string? tableName, int? userId);
    }
}