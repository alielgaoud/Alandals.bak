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

        // 1. تسجيل دخول الموظفين والإدارة
        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserName == dto.UserName && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("البريد أو كلمة المرور غير صحيحة");

            // 🛑 منع المستأجرين من الدخول إلى لوحة تحكم الإدارة
            if (user.Role == UserRole.Tenant)
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

        // 2. تسجيل دخول المستأجرين (مؤمن ومحمي)
        public async Task<TenantAuthResponseDto> TenantLoginAsync(LoginDto dto)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserName == dto.UserName && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("البريد الإلكتروني أو كلمة المرور غير صحيحة");

            // 🛑 التحقق من الدور: يجب أن يكون مستأجراً حصراً ولديه TenantId مرتبطه بحسابه
            if (user.Role != UserRole.Tenant || !user.TenantId.HasValue)
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
                Role = "Tenant",
                TenantId = user.TenantId.Value, // إرسال الـ ID للأنجولر
                Expiration = DateTime.UtcNow.AddMinutes(60)
            };
        }

        // تابع مساعد للتحقق من كلمة المرور ونظام القفل التلقائي لمنع الهجمات
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
                throw new UnauthorizedAccessException("البريد الإلكتروني أو كلمة المرور غير صحيحة");
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
                throw new InvalidOperationException("هذا البريد مسجل مسبقاً");

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