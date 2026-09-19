using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Andalos.API.Seed
{
    public static class UserSeeder
    {
        public static async Task SeedAsync(AppDbContext db)
        {
            // التحقق مما إذا كان الحساب الرئيسي موجوداً أم لا
            var exists = await db.Users.AnyAsync(u => u.UserName == SystemConstants.SuperAdminUserName);
            if (!exists)
            {
                var superAdmin = new User
                {
                    FullName = SystemConstants.SuperAdminFullName,
                    UserName = SystemConstants.SuperAdminUserName,
                    PasswordHash = HashPassword(SystemConstants.SuperAdminDefaultPassword),
                    Phone = "0910000000",
                    Role = UserRole.SuperAdmin, // 👈 يمتلك صلاحيات مطلقة على النظام بالكامل
                    IsActive = true,
                    IsLocked = false
                };

                db.Users.Add(superAdmin);
                await db.SaveChangesAsync();
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}