using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Models;
using Andalos.API.Security;
using Microsoft.EntityFrameworkCore;
namespace Andalos.API.Seed;
public static class UserSeeder
{
    // One-shot OFFLINE operator command, never enabled by an HTTP request or a normal application startup.
    public static async Task BootstrapAsync(AppDbContext db, IConfiguration config, PasswordService passwords, bool rotate)
    {
        var name = config["BootstrapSuperAdmin:UserName"]?.Trim();
        var password = config["BootstrapSuperAdmin:Password"];
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password)) throw new InvalidOperationException("Supply bootstrap identity and password through deployment secrets.");
        PasswordService.ValidateNew(password);
        await db.AtomicAsync(async () =>
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.UserName == name);
            if (rotate)
            {
                if (user is null || user.Role != UserRole.SuperAdmin) throw new InvalidOperationException("Offline rotation may only target an existing SuperAdmin, never promote an account.");
            }
            else
            {
                if (user is not null || await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin && u.IsActive && !u.IsLocked))
                    throw new InvalidOperationException("Bootstrap is only allowed when no accessible SuperAdmin exists and username is new.");
                user = new User { UserName = name, FullName = "System administrator", Role = UserRole.SuperAdmin };
                db.Users.Add(user);
            }
            user!.PasswordHash = passwords.Hash(user, password); user.IsActive = true; user.IsLocked = false;
            user.LockoutEnd = null; user.FailedLoginAttempts = 0; user.RequiresPasswordChange = false; user.PermissionsReconciled = true;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
            db.AuditLogs.Add(new() { AuditType = rotate ? "OfflineRotate" : "OfflineBootstrap", TableName = "SuperAdmin", PrimaryKey = user.Id.ToString(),
                Outcome = "Success", NewValues = "{\"operator\":\"deployment-cli\"}" });
            await db.SaveChangesAsync();
        });
    }
}
