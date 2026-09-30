using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.System;
using Andalos.API.DTOs.Users;
using Andalos.API.Enums;
using Andalos.API.Helpers;
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
        public async Task<UserPermissionsResponseDto?> GetUserPermissionsAsync(
      int userId)
        {
            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    u.Id == userId && u.IsActive);

            if (user == null)
                return null;

            var direct = await _db.UserPermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.IsActive)
                .Select(p => p.PermissionKey)
                .ToListAsync();

            var fromPackages = await _db.UserPermissionPackages
                .AsNoTracking()
                .Where(link =>
                    link.UserId == userId &&
                    link.IsActive &&
                    link.Package != null &&
                    link.Package.IsActive)
                .SelectMany(link =>
                    link.Package!.Items.Select(item => item.PermissionKey))
                .ToListAsync();

            direct = direct
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            fromPackages = fromPackages
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            var effective = direct
                .Concat(fromPackages)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            var knownKeys = Permissions.GetAllPermissions()
                .ToHashSet(StringComparer.Ordinal);

            var legacy = effective
                .Where(key => !knownKeys.Contains(key))
                .ToList();

            // التداخل التاريخي يحتاج مراجعة: المزامنة القديمة ربما
            // نسخت مفتاح الباقة إلى جدول UserPermissions أيضاً.
            var overlap = direct
                .Intersect(fromPackages, StringComparer.Ordinal)
                .Any();

            return new UserPermissionsResponseDto
            {
                UserId = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                GrantedPermissions = direct,
                PackagePermissions = fromPackages,
                EffectivePermissions = effective,
                LegacyPermissions = legacy,
                ReconciliationRequired = legacy.Count > 0 || overlap,
                PermissionsVersion = user.PermissionsVersion
            };
        }

        public async Task<bool> AssignPermissionsAsync(
     AssignUserPermissionsDto dto)
        {
            if (dto.Permissions == null ||
                !dto.ExpectedVersion.HasValue ||
                dto.ExpectedVersion.Value < 0)
            {
                throw new InvalidOperationException(
                    "يجب إرسال permissions و expectedVersion صراحة");
            }

            var requested = dto.Permissions
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            var knownKeys = Permissions.GetAllPermissions()
                .ToHashSet(StringComparer.Ordinal);

            if (requested.Any(key => !knownKeys.Contains(key)))
            {
                throw new InvalidOperationException(
                    "توجد مفاتيح صلاحيات غير معروفة للخادم؛ لم يُحفظ أي تعديل");
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == dto.UserId && u.IsActive);

            if (user == null)
                return false;

            if (IsProtectedSystemUser(user) ||
                user.Role == UserRole.SuperAdmin)
            {
                throw new InvalidOperationException(
                    "لا يمكن تعديل منح مدير النظام");
            }

            if (user.PermissionsVersion != dto.ExpectedVersion.Value)
                throw new PermissionVersionConflictException();

            // اجلب النشط وغير النشط حتى يمكن إعادة تنشيط سجل قديم
            // بدلاً من إنشاء سجل مكرر يصطدم بقيد فريد في قاعدة البيانات.
            var existing = await _db.UserPermissions
                .Where(p => p.UserId == dto.UserId)
                .ToListAsync();

            foreach (var permission in existing)
            {
                permission.IsActive =
                    requested.Contains(permission.PermissionKey);
            }

            var existingKeys = existing
                .Select(p => p.PermissionKey)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in requested.Except(
                         existingKeys, StringComparer.Ordinal))
            {
                _db.UserPermissions.Add(new UserPermission
                {
                    UserId = dto.UserId,
                    PermissionKey = key,
                    IsActive = true
                });
            }

            // [ConcurrencyCheck] يجعل تحديث User مشروطاً بالإصدار
            // الذي قرأته هذه العملية. SaveChanges يحفظ التغيير كوحدة واحدة.
            user.PermissionsVersion++;
            user.UpdatedAt = DateTimeHelper.LibyaNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _db.ChangeTracker.Clear();
                throw new PermissionVersionConflictException(ex);
            }

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
            user.UpdatedAt = DateTimeHelper.LibyaNow;

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
            user.UpdatedAt = DateTimeHelper.LibyaNow;

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
                user.LockoutEnd = DateTimeHelper.LibyaNow.AddYears(10);
            }
            user.UpdatedAt = DateTimeHelper.LibyaNow;

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
            user.UpdatedAt = DateTimeHelper.LibyaNow;
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