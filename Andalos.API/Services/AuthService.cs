using Andalos.API.Data;
using Andalos.API.DTOs.Auth;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
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

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserName == dto.UserName && u.IsActive);

            if (user == null)
                throw new UnauthorizedAccessException("البريد أو كلمة المرور غير صحيحة");

            if (user.IsLocked)
            {
                if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
                    throw new UnauthorizedAccessException("الحساب مقفل مؤقتاً، حاول لاحقاً");
                else
                {
                    user.IsLocked = false;
                    user.FailedLoginAttempts = 0;
                    user.LockoutEnd = null;
                }
            }

            if (!VerifyPassword(dto.Password, user.PasswordHash))
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.IsLocked = true;
                    user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                }
                await _db.SaveChangesAsync();
                throw new UnauthorizedAccessException("البريد أو كلمة المرور غير صحيحة");
            }

            // نجاح الدخول
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockoutEnd = null;
            user.LastLoginAt = DateTime.UtcNow;
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

        // ===== Hashing بسيط وآمن =====
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