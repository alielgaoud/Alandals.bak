using Andalos.API.Data;
using Andalos.API.DTOs.Users;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Andalos.API.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;

        public UserService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<UserResponseDto>> GetAllAsync()
        {
            return await _db.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => MapToDto(u))
                .ToListAsync();
        }

        public async Task<UserResponseDto?> GetByIdAsync(int id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            return user == null ? null : MapToDto(user);
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserByAdminDto dto)
        {
            // 👈 التحديث للتحقق من عدم تكرار اسم المستخدم
            var exists = await _db.Users.AnyAsync(u => u.UserName == dto.UserName && u.IsActive);
            if (exists)
                throw new InvalidOperationException("اسم المستخدم مسجل مسبقاً لمستخدم آخر");

            var user = new User
            {
                FullName = dto.FullName,
                UserName = dto.UserName, // 👈 تم التحديث
                Phone = dto.Phone,
                PasswordHash = HashPassword(dto.Password),
                Role = dto.Role,
                IsActive = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return MapToDto(user);
        }

        public async Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return null;

            // 👈 التحديث للتحقق من عدم تكرار اسم المستخدم الجديد
            var userNameConflict = await _db.Users.AnyAsync(u => u.UserName == dto.UserName && u.Id != id && u.IsActive);
            if (userNameConflict)
                throw new InvalidOperationException("اسم المستخدم الجديد مسجل مسبقاً لمستخدم آخر");

            user.FullName = dto.FullName;
            user.UserName = dto.UserName; // 👈 تم التحديث
            user.Phone = dto.Phone;
            user.Role = dto.Role;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return MapToDto(user);
        }

        public async Task<bool> ResetPasswordAsync(int id, string newPassword)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return false;

            user.PasswordHash = HashPassword(newPassword);
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockoutEnd = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleLockAccountAsync(int id, bool lockAccount)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return false;

            user.IsLocked = lockAccount;
            if (!lockAccount)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = null;
            }
            else
            {
                user.LockoutEnd = DateTime.UtcNow.AddYears(10);
            }
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return false;

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static UserResponseDto MapToDto(User u)
        {
            return new UserResponseDto
            {
                Id = u.Id,
                FullName = u.FullName,
                UserName = u.UserName, // 👈 تم التحديث
                Phone = u.Phone,
                Role = u.Role.ToString(),
                IsLocked = u.IsLocked,
                FailedLoginAttempts = u.FailedLoginAttempts,
                LastLoginAt = u.LastLoginAt,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            };
        }
    }
}