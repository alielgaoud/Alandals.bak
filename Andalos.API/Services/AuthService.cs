using Andalos.API.Data;
using Andalos.API.DTOs.Auth;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Andalos.API.Enums;
using Andalos.API.Constants;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Andalos.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly JwtHelper _jwt;
        private readonly IPermissionPackageService _permissionService; // 👈 حقن خدمة الصلاحيات

        public AuthService(AppDbContext db, JwtHelper jwt, IPermissionPackageService permissionService)
        {
            _db = db;
            _jwt = jwt;
            _permissionService = permissionService;
        }

        // =====================================================
        // 1. تسجيل دخول الموظفين والإدارة (اسم مستخدم / بريد / هاتف)
        // =====================================================
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var input = dto.UserName.Trim();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => (u.UserName == input
                                        || u.UserName.ToLower() == input.ToLower()
                                        || u.Phone == input)
                                       && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("اسم المستخدم / البريد الإلكتروني أو كلمة المرور غير صحيحة");

            if (user.Role == UserRole.Tenant || user.Role == UserRole.TenantStaff)
                throw new UnauthorizedAccessException("غير مصرح لك بالدخول من هنا، يرجى استخدام بوابة المستأجرين");

            await CheckAndApplyLockoutAsync(user, dto.Password);

            // 👈 1. جلب الصلاحيات الفعالة للمستخدم (مباشرة + باقات الصلاحيات الممنوحة له)
            List<string> permissions;
            List<string> modules;

            if (user.Role == UserRole.SuperAdmin)
            {
                // الـ SuperAdmin يملك كافة صلاحيات وأقسام النظام تلقائياً وضمنياً
                permissions = Permissions.GetAllPermissions();
                modules = PermissionModules.ModuleMap.Keys.ToList();
            }
            else
            {
                permissions = await _permissionService.GetEffectivePermissionsForUserAsync(user.Id);

                // استنتاج الـ Modules المسموحة بناءً على الصلاحيات التي يمتلكها
                modules = PermissionModules.ModuleMap
                    .Where(m => m.Value.Any(k => permissions.Contains(k)))
                    .Select(m => m.Key)
                    .ToList();
            }

            var token = _jwt.GenerateToken(
     user, permissions, out var expirationUtc);

            return new AuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                UserName = user.UserName,
                Role = user.Role.ToString(),
                Expiration = expirationUtc,
                Permissions = permissions, // 👈 إرجاعها في الـ Response للفرونت اند
                Modules = modules          // 👈 إرجاع الأقسام المسموحة للفرونت اند
            };
        }

        // =====================================================
        // 2. تسجيل دخول المستأجرين (اسم مستخدم / بريد / هاتف)
        // =====================================================
        public async Task<TenantAuthResponseDto> TenantLoginAsync(LoginDto dto)
        {
            var input = dto.UserName.Trim();

            var user = await _db.Users
                .FirstOrDefaultAsync(u => (u.UserName == input
                                        || u.UserName.ToLower() == input.ToLower()
                                        || u.Phone == input)
                                       && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("اسم المستخدم / البريد الإلكتروني أو كلمة المرور غير صحيحة");

            if ((user.Role != UserRole.Tenant && user.Role != UserRole.TenantStaff) || !user.TenantId.HasValue)
                throw new UnauthorizedAccessException("هذا الحساب ليس حساب مستأجر مسجل");

            await CheckAndApplyLockoutAsync(user, dto.Password);

            // 👈 1. تحديد صلاحيات المستأجر الافتراضية والآمنة
            var tenantPermissions = new List<string>
            {
                "Contracts.View",
                "Financials.ViewPayments",
                "Complaints.View",
                "Complaints.Create",
                "VisitorWallet.ViewMyBalance",
                "Maintenance.View",
                "Maintenance.Create",
                "Circulars.View"
            };

            // 👈 2. توليد التوكن شاملاً صلاحيات المستأجر
            var token = _jwt.GenerateToken(
                user, tenantPermissions, out var expirationUtc);
            return new TenantAuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                UserName = user.UserName,
                Role = user.Role.ToString(),
                TenantId = user.TenantId.Value,
                Expiration = expirationUtc,
                Permissions = tenantPermissions // 👈 إرجاعها في الـ Response
            };
        }

        private async Task CheckAndApplyLockoutAsync(User user, string password)
        {
            if (user.IsLocked)
            {
                if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeHelper.LibyaNow)
                {
                    var remaining = Math.Ceiling((user.LockoutEnd.Value - DateTimeHelper.LibyaNow).TotalMinutes);
                    throw new UnauthorizedAccessException($"الحساب مقفل مؤقتاً. حاول مجدداً بعد {remaining} دقيقة");
                }
                else
                {
                    user.IsLocked = false;
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = null;
                }
            }

            if (!VerifyPassword(password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.IsLocked = true;
                    user.LockoutEnd = DateTimeHelper.LibyaNow.AddMinutes(15);
                    await _db.SaveChangesAsync();
                    throw new UnauthorizedAccessException("تم قفل الحساب لمدة 15 دقيقة بسبب محاولات دخول خاطئة متعددة");
                }

                await _db.SaveChangesAsync();
                throw new UnauthorizedAccessException("اسم المستخدم / البريد الإلكتروني أو كلمة المرور غير صحيحة");
            }

            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var exists = await _db.Users.AnyAsync(u => u.UserName == dto.UserName);
            if (exists)
                throw new InvalidOperationException("هذا الحساب مسجل مسبقاً");

            var user = new User
            {
                FullName = dto.FullName,
                UserName = dto.UserName,
                PasswordHash = HashPassword(dto.Password),
                Phone = dto.Phone,
                Role = dto.Role
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // عند تسجيل مستخدم جديد لا يمتلك أي صلاحيات بعد حتى يتم تعيينها له من لوحة التحكم
            var emptyPermissions = new List<string>();
            var token = _jwt.GenerateToken(
                user, emptyPermissions, out var expirationUtc);
            return new AuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                UserName = user.UserName,
                Role = user.Role.ToString(),
                Expiration = expirationUtc,
                Permissions = emptyPermissions,
                Modules = new List<string>()
            };
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}