using Andalos.API.Data;
using Andalos.API.DTOs.Auth;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Andalos.API.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Andalos.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly JwtHelper _jwt;

        public AuthService(AppDbContext db, JwtHelper jwt)
        {
            _db = db;
            _jwt = jwt;
        }

        // =====================================================
        // 1. تسجيل دخول الموظفين والإدارة (اسم مستخدم / بريد / هاتف)
        // =====================================================
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var input = dto.UserName.Trim();

            // 👈 مطابقة مرنة: اسم المستخدم أو البريد أو رقم الهاتف
            var user = await _db.Users
                .FirstOrDefaultAsync(u => (u.UserName == input
                                        || u.UserName.ToLower() == input.ToLower()
                                        || u.Phone == input)
                                       && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("اسم المستخدم / البريد الإلكتروني أو كلمة المرور غير صحيحة");

            // 🛑 منع المستأجرين من الدخول إلى لوحة تحكم الإدارة
            if (user.Role == UserRole.Tenant || user.Role == UserRole.TenantStaff)
                throw new UnauthorizedAccessException("غير مصرح لك بالدخول من هنا، يرجى استخدام بوابة المستأجرين");

            await CheckAndApplyLockoutAsync(user, dto.Password);

            var token = _jwt.GenerateToken(user);

            return new AuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                UserName = user.UserName,
                Role = user.Role.ToString(),
                Expiration = DateTime.UtcNow.AddMinutes(60)
            };
        }

        // =====================================================
        // 2. تسجيل دخول المستأجرين (اسم مستخدم / بريد / هاتف)
        // =====================================================
        public async Task<TenantAuthResponseDto> TenantLoginAsync(LoginDto dto)
        {
            var input = dto.UserName.Trim();

            // 👈 مطابقة مرنة: اسم المستخدم أو البريد أو رقم الهاتف
            var user = await _db.Users
                .FirstOrDefaultAsync(u => (u.UserName == input
                                        || u.UserName.ToLower() == input.ToLower()
                                        || u.Phone == input)
                                       && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("اسم المستخدم / البريد الإلكتروني أو كلمة المرور غير صحيحة");

            // 🛑 التحقق من الدور: يجب أن يكون مستأجراً أو موظف مستأجر ومرتبط بـ TenantId
            if ((user.Role != UserRole.Tenant && user.Role != UserRole.TenantStaff) || !user.TenantId.HasValue)
                throw new UnauthorizedAccessException("هذا الحساب ليس حساب مستأجر مسجل");

            // التحقق من حالة القفل والتخمين
            await CheckAndApplyLockoutAsync(user, dto.Password);

            // توليد الـ JWT Token متضمناً الـ TenantId Claim
            var token = _jwt.GenerateToken(user);

            return new TenantAuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                UserName = user.UserName,
                Role = user.Role.ToString(),
                TenantId = user.TenantId.Value,
                Expiration = DateTime.UtcNow.AddMinutes(60)
            };
        }

        // تابع مساعد للتحقق من كلمة المرور ونظام القفل التلقائي
        private async Task CheckAndApplyLockoutAsync(User user, string password)
        {
            if (user.IsLocked)
            {
                if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
                    throw new UnauthorizedAccessException($"الحساب مقفل مؤقتاً. حاول مجدداً بعد {Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes)} دقيقة");
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
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15); // قفل الحساب 15 دقيقة
                    await _db.SaveChangesAsync();
                    throw new UnauthorizedAccessException("تم قفل الحساب لمدة 15 دقيقة بسبب محاولات دخول خاطئة متعددة");
                }

                await _db.SaveChangesAsync();
                throw new UnauthorizedAccessException("اسم المستخدم / البريد الإلكتروني أو كلمة المرور غير صحيحة");
            }

            // نجاح الدخول - تصفير العدادات
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;
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

            var token = _jwt.GenerateToken(user);

            return new AuthResponseDto
            {
                Token = token,
                FullName = user.FullName,
                UserName = user.UserName,
                Role = user.Role.ToString(),
                Expiration = DateTime.UtcNow.AddMinutes(60)
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