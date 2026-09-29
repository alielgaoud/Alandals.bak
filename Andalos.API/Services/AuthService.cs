using Andalos.API.Data;
using Andalos.API.DTOs.Auth;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Andalos.API.Security;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services;
public sealed class AuthService(AppDbContext db, JwtHelper jwt, PasswordService passwords,
    UserSnapshotReader snapshots, EffectivePermissions permissions) : IAuthService
{
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await AuthenticateAsync(dto, false);
        var token = jwt.GenerateToken(user, out var expiration);
        var snapshot = await snapshots.ReadAsync(user.Id) ?? throw new UnauthorizedAccessException();
        var keys = user.RequiresPasswordChange ? Array.Empty<string>() : await permissions.GetAsync(snapshot);
        return new() { Token = token, FullName = user.FullName, UserName = user.UserName, Role = user.Role.ToString(),
            Expiration = expiration, Permissions = keys.ToList(), Modules = Andalos.API.Constants.Permissions.ModulesFor(keys),
            PermissionsVersion = snapshot.PermissionsVersion, ReconciliationRequired = !snapshot.PermissionsReconciled,
            RequiresPasswordChange = user.RequiresPasswordChange };
    }
    public async Task<TenantAuthResponseDto> TenantLoginAsync(LoginDto dto)
    {
        var user = await AuthenticateAsync(dto, true);
        var token = jwt.GenerateToken(user, out var expiration);
        return new() { Token = token, FullName = user.FullName, UserName = user.UserName, Role = user.Role.ToString(),
            TenantId = user.TenantId!.Value, Expiration = expiration, RequiresPasswordChange = user.RequiresPasswordChange };
    }
    private async Task<User> AuthenticateAsync(LoginDto dto, bool tenant)
    {
        // Rejected logins must COMMIT their failure counters. Throw only after the transaction returns.
        var user = await db.AtomicAsync<User?>(async () =>
        {
            var input = dto.UserName.Trim();
            var u = await db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.IsActive && (u.UserName == input || u.Phone == input));
            if (u is null) { passwords.VerifyDummy(dto.Password); return null; }
            if (u.IsLocked)
            {
                if (u.LockoutEnd is null || u.LockoutEnd > DateTimeHelper.LibyaNow) { passwords.VerifyDummy(dto.Password); return null; }
                u.IsLocked = false; u.LockoutEnd = null; u.FailedLoginAttempts = 0;
            }
            var validIdentity = tenant
                ? (u.Role is UserRole.Tenant or UserRole.TenantStaff) && u.TenantId.HasValue && u.Tenant?.IsActive == true
                : u.Role is UserRole.SuperAdmin or UserRole.Admin or UserRole.Accountant or UserRole.GateKeeper;
            if (!validIdentity) { passwords.VerifyDummy(dto.Password); return null; }
            if (!passwords.Verify(u, dto.Password, out var upgrade))
            {
                u.FailedLoginAttempts++;
                if (u.FailedLoginAttempts >= 5) { u.IsLocked = true; u.LockoutEnd = DateTimeHelper.LibyaNow.AddMinutes(15); }
                await db.SaveChangesAsync(); return null;
            }
            if (upgrade) u.PasswordHash = passwords.Upgrade(u, dto.Password);
            u.FailedLoginAttempts = 0; u.IsLocked = false; u.LockoutEnd = null; u.LastLoginAt = DateTimeHelper.LibyaNow;
            await db.SaveChangesAsync(); return u;
        });
        return user ?? throw new UnauthorizedAccessException("اسم المستخدم أو كلمة المرور غير صحيحة، أو الحساب غير متاح.");
    }
    public Task<AuthResponseDto> RegisterAsync(RegisterDto dto) => throw new ForbiddenOperationException(); // No public role-bearing registration, even via service.
}
