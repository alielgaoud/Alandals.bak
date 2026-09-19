using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.System;
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

        // 👈 دالة مساعدة لفحص ما إذا كان المستخدم هو الحساب الافتراضي المحمي
        private static bool IsProtectedSystemUser(User user)
        {
            return user.UserName.Equals(SystemConstants.SuperAdminUserName, StringComparison.OrdinalIgnoreCase);
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

        // 👈 جلب الصلاحيات الممنوحة لمستخدم معين
        public async Task<UserPermissionsResponseDto?> GetUserPermissionsAsync(int userId)
        {
            var user = await _db.Users
                .Include(u => u.Permissions)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

            if (user == null) return null;

            return new UserPermissionsResponseDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                GrantedPermissions = user.Permissions.Select(p => p.PermissionKey).ToList()
            };
        }

        // 👈 تحديث الصلاحيات الممنوحة للمستخدم (حذف القديم وإضافة الجديد)
        public async Task<bool> AssignPermissionsAsync(AssignUserPermissionsDto dto)
        {
            var user = await _db.Users
                .Include(u => u.Permissions)
                .FirstOrDefaultAsync(u => u.Id == dto.UserId && u.IsActive);

            if (user == null) return false;

            // منع المساس بصلاحيات مدير النظام الافتراضي المحمي
            if (IsProtectedSystemUser(user))
                throw new InvalidOperationException("❌ غير مسموح: مدير النظام الرئيسي يمتلك كافة الصلاحيات ضمناً ولا يمكن تعديلها.");

            // حذف الصلاحيات القديمة
            _db.UserPermissions.RemoveRange(user.Permissions);

            // التحقق من أن الصلاحيات المدخلة صحيحة وموجودة فعلياً في النظام
            var validPermissions = Permissions.GetAllPermissions();

            // إضافة الصلاحيات الجديدة
            foreach (var permission in dto.Permissions)
            {
                if (validPermissions.Contains(permission))
                {
                    _db.UserPermissions.Add(new UserPermission
                    {
                        UserId = dto.UserId,
                        PermissionKey = permission
                    });
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<AuditLogDto>> GetAuditLogsAsync(DateTime? fromDate, DateTime? toDate, string? tableName, int? userId)
        {
            var query = _db.AuditLogs.Include(a => a.User).AsQueryable();

            if (fromDate.HasValue) query = query.Where(a => a.CreatedAt >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(a => a.CreatedAt <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            if (!string.IsNullOrEmpty(tableName)) query = query.Where(a => a.TableName == tableName);
            if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);

            return await query
                .OrderByDescending(a => a.CreatedAt)
                .Take(200) // جلب آخر 200 حركة كحد أقصى لحماية الأداء
                .Select(a => new AuditLogDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.User != null ? a.User.FullName : "نظام آلي",
                    AuditType = a.AuditType,
                    TableName = a.TableName,
                    PrimaryKey = a.PrimaryKey,
                    OldValues = a.OldValues,
                    NewValues = a.NewValues,
                    AffectedColumns = a.AffectedColumns,
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserByAdminDto dto)
        {
            var exists = await _db.Users.AnyAsync(u => u.UserName == dto.UserName && u.IsActive);
            if (exists)
                throw new InvalidOperationException("اسم المستخدم مسجل مسبقاً لمستخدم آخر");

            var user = new User
            {
                FullName = dto.FullName,
                UserName = dto.UserName,
                Phone = dto.Phone,
                PasswordHash = HashPassword(dto.Password),
                Role = dto.Role,
                IsActive = true
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return MapToDto(user);
        }

        // 👈 1. منع تعديل الحساب الافتراضي
        public async Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return null;

            if (IsProtectedSystemUser(user))
                throw new InvalidOperationException("❌ غير مسموح: حساب مدير النظام الرئيسي محمي ولا يمكن تعديله أبداً.");

            var userNameConflict = await _db.Users.AnyAsync(u => u.UserName == dto.UserName && u.Id != id && u.IsActive);
            if (userNameConflict)
                throw new InvalidOperationException("اسم المستخدم الجديد مسجل مسبقاً لمستخدم آخر");

            user.FullName = dto.FullName;
            user.UserName = dto.UserName;
            user.Phone = dto.Phone;
            user.Role = dto.Role;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return MapToDto(user);
        }

        // 👈 2. منع تغيير كلمة مرور الحساب الافتراضي
        public async Task<bool> ResetPasswordAsync(int id, string newPassword)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return false;

            if (IsProtectedSystemUser(user))
                throw new InvalidOperationException("❌ غير مسموح: لا يمكن تغيير كلمة مرور حساب مدير النظام الرئيسي الافتراضي.");

            user.PasswordHash = HashPassword(newPassword);
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockoutEnd = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return true;
        }

        // 👈 3. منع قفل الحساب الافتراضي
        public async Task<bool> ToggleLockAccountAsync(int id, bool lockAccount)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return false;

            if (IsProtectedSystemUser(user))
                throw new InvalidOperationException("❌ غير مسموح: لا يمكن قفل حساب مدير النظام الرئيسي الافتراضي.");

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

        // 👈 4. منع حذف الحساب الافتراضي
        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (user == null) return false;

            if (IsProtectedSystemUser(user))
                throw new InvalidOperationException("❌ غير مسموح: لا يمكن حذف حساب مدير النظام الرئيسي الافتراضي.");

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<UserResponseDto>> GetUsersByTenantIdAsync(int tenantId)
        {
            return await _db.Users
                .Where(u => u.TenantId == tenantId && u.IsActive)
                .OrderBy(u => u.FullName)
                .Select(u => MapToDto(u))
                .ToListAsync();
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
                UserName = u.UserName,
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