using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.System;
using Andalos.API.DTOs.Users;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Andalos.API.Security;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Andalos.API.Services;
public sealed class UserService(AppDbContext db, DelegationGuard guard, PasswordService passwords,
    UserSnapshotReader snapshots, EffectivePermissions effective, PermissionPackageService packages) : IUserService
{
    public async Task<List<UserResponseDto>> GetAllAsync() => (await db.Users.AsNoTracking().Where(u => u.IsActive)
        .OrderBy(u => u.FullName).ToListAsync()).Select(Map).ToList();
    public async Task<UserResponseDto?> GetByIdAsync(int id)
    {
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
        return u is null ? null : Map(u);
    }
    public async Task<UserPermissionsResponseDto?> GetUserPermissionsAsync(int id)
    {
        var user = await snapshots.ReadAsync(id);
        if (user is null) return null;
        var direct = await db.DirectUserPermissions.Where(p => p.UserId == id && p.IsActive).Select(p => p.PermissionKey).ToListAsync();
        var packageKeys = await db.UserPermissionPackages.Where(x => x.UserId == id && x.IsActive && x.Package!.IsActive)
            .SelectMany(x => x.Package!.Items.Where(i => i.IsActive).Select(i => i.PermissionKey)).Distinct().ToListAsync();
        var legacy = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == id && p.IsActive).Select(p => p.PermissionKey).ToListAsync();
        return new() { UserId = id, UserName = user.UserName, FullName = user.FullName,
            GrantedPermissions = direct.Order(StringComparer.Ordinal).ToList(), PackagePermissions = packageKeys.Order(StringComparer.Ordinal).ToList(),
            EffectivePermissions = (await effective.GetAsync(user)).ToList(), LegacyPermissions = legacy.Order(StringComparer.Ordinal).ToList(),
            ReconciliationRequired = !user.PermissionsReconciled, PermissionsVersion = user.PermissionsVersion };
    }
    public Task<bool> AssignPermissionsAsync(AssignUserPermissionsDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(dto.UserId);
        var keys = Permissions.Validate(dto.Permissions); // reject, never silently filter unknown keys
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == dto.UserId && u.IsActive);
        if (user is null) return false;
        DelegationGuard.RequireEmployeeTarget(user); DelegationGuard.CheckVersion(user, dto.ExpectedVersion);
        if (!user.PermissionsReconciled) throw new ArgumentException("Explicit legacy reconciliation is required first.");
        await ReplaceDirectAsync(user.Id, keys);
        await db.SaveChangesAsync(); return true;
    });
    private async Task ReplaceDirectAsync(int id, List<string> keys)
    {
        var old = await db.DirectUserPermissions.Where(p => p.UserId == id).ToListAsync();
        foreach (var grant in old)
            if (!keys.Contains(grant.PermissionKey, StringComparer.Ordinal)) db.DirectUserPermissions.Remove(grant);
            else grant.IsActive = true;
        foreach (var key in keys.Where(k => !old.Any(p => p.PermissionKey == k))) db.DirectUserPermissions.Add(new() { UserId = id, PermissionKey = key });
    }
    public Task<bool> ReconcilePermissionsAsync(int id, ReconcilePermissionsDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(id);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (user is null) return false;
        DelegationGuard.RequireEmployeeTarget(user);
        if (dto.ExpectedVersion is null || dto.ExpectedVersion != user.PermissionsVersion) throw new ConcurrencyConflictException();
        if (user.PermissionsReconciled) throw new ArgumentException("This user is already reconciled; use normal grants/assignments.");
        if (string.IsNullOrWhiteSpace(dto.ReviewNote) || dto.ReviewNote.Length < 10 || dto.ReviewNote.Length > 500) throw new ArgumentException("A review reference is required.");
        var direct = Permissions.Validate(dto.DirectPermissions);
        var legacy = await db.UserPermissions.Where(p => p.UserId == id && p.IsActive).Select(p => p.PermissionKey).ToListAsync();
        if (dto.LegacyDecisions is null || dto.LegacyDecisions.Count != legacy.Count ||
            !dto.LegacyDecisions.Select(d => d.PermissionKey).ToHashSet(StringComparer.Ordinal).SetEquals(legacy))
            throw new ArgumentException("Every legacy key must receive one explicit provenance decision.");
        if (dto.PackageIds is null) throw new ArgumentException("packageIds required.");
        var ids = dto.PackageIds.Distinct().ToArray();
        var packageKeys = await db.PermissionPackageItems.Where(i => i.IsActive && i.Package!.IsActive && ids.Contains(i.PackageId))
            .Select(i => i.PermissionKey).Distinct().ToListAsync();
        foreach (var d in dto.LegacyDecisions)
        {
            if (d.Source == "Direct" && direct.Contains(d.PermissionKey, StringComparer.Ordinal) && Permissions.IsKnown(d.PermissionKey)) continue;
            if (d.Source == "Package" && packageKeys.Contains(d.PermissionKey, StringComparer.Ordinal) && !direct.Contains(d.PermissionKey, StringComparer.Ordinal)) continue;
            if (d.Source == "Remove" && !direct.Contains(d.PermissionKey, StringComparer.Ordinal) && !packageKeys.Contains(d.PermissionKey, StringComparer.Ordinal)) continue;
            throw new ArgumentException("Invalid provenance decision; Remove is not a Deny over an active package.");
        }
        await ReplaceDirectAsync(id, direct);
        await packages.ReplaceAssignmentsAsync(id, dto.PackageIds);
        user.PermissionsReconciled = true;
        db.AuditLogs.Add(new() { UserId = guard.ActorId, AuditType = "Reconcile", TableName = "EmployeePermissions", PrimaryKey = id.ToString(), CorrelationId = db.RequestCorrelationId,
            NewValues = JsonSerializer.Serialize(new { dto.DirectPermissions, dto.PackageIds, dto.LegacyDecisions, dto.ReviewNote }),
            CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); return true;
    });
    public Task<UserResponseDto> CreateUserAsync(CreateUserByAdminDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin();
        if (!Enum.IsDefined(dto.Role) || dto.Role is UserRole.Tenant or UserRole.TenantStaff) throw new ArgumentException("Use the separate tenant account endpoint.");
        if (await db.Users.AnyAsync(u => u.UserName == dto.UserName.Trim())) throw new ArgumentException("Username already exists.");
        var u = new User { FullName = dto.FullName.Trim(), UserName = dto.UserName.Trim(), Phone = dto.Phone, Role = dto.Role };
        u.PasswordHash = passwords.Hash(u, dto.Password);
        db.Users.Add(u); await db.SaveChangesAsync(); return Map(u);
    });
    public Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(id);
        var u = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (u is null) return null;
        if (!Enum.IsDefined(dto.Role) || (u.Role is UserRole.Tenant or UserRole.TenantStaff) != (dto.Role is UserRole.Tenant or UserRole.TenantStaff))
            throw new ArgumentException("Changing an account between employee and tenant identities is forbidden.");
        if (u.Role == UserRole.SuperAdmin && (!dto.IsActive || dto.Role != UserRole.SuperAdmin)) await EnsureAnotherSuperAdminAsync(id);
        if (await db.Users.AnyAsync(x => x.UserName == dto.UserName.Trim() && x.Id != id)) throw new ArgumentException("Username already exists.");
        u.FullName = dto.FullName.Trim(); u.UserName = dto.UserName.Trim(); u.Phone = dto.Phone; u.Role = dto.Role; u.IsActive = dto.IsActive;
        u.UpdatedAt = DateTimeHelper.LibyaNow;
        await db.SaveChangesAsync(); return Map(u); // SecurityStamp is rotated atomically by DbContext.
    });
    public Task<bool> ResetPasswordAsync(int id, string password) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(id);
        var u = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (u is null) return false;
        u.PasswordHash = passwords.Hash(u, password); u.IsLocked = false; u.LockoutEnd = null; u.FailedLoginAttempts = 0;
        u.RequiresPasswordChange = true; u.UpdatedAt = DateTimeHelper.LibyaNow;
        await db.SaveChangesAsync(); return true;
    });
    public Task<bool> ToggleLockAccountAsync(int id, bool locked) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(id);
        var u = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (u is null) return false;
        if (locked && u.Role == UserRole.SuperAdmin) await EnsureAnotherSuperAdminAsync(id);
        u.IsLocked = locked; u.LockoutEnd = null; // null is a permanent administrative lock, not auto-unlock.
        u.FailedLoginAttempts = 0; u.UpdatedAt = DateTimeHelper.LibyaNow;
        await db.SaveChangesAsync(); return true;
    });
    public Task<bool> DeleteUserAsync(int id) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(id);
        var u = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
        if (u is null) return false;
        if (u.Role == UserRole.SuperAdmin) await EnsureAnotherSuperAdminAsync(id);
        u.IsActive = false; u.UpdatedAt = DateTimeHelper.LibyaNow;
        await db.SaveChangesAsync(); return true;
    });
    private async Task EnsureAnotherSuperAdminAsync(int id)
    {
        if (!await db.Users.AnyAsync(u => u.Id != id && u.Role == UserRole.SuperAdmin && u.IsActive && !u.IsLocked && !u.RequiresPasswordChange)) throw new ForbiddenOperationException();
    }
    public async Task<List<UserResponseDto>> GetUsersByTenantIdAsync(int id) => (await db.Users.AsNoTracking().Where(u => u.TenantId == id && u.IsActive).ToListAsync()).Select(Map).ToList();
    public async Task<List<AuditLogDto>> GetAuditLogsAsync(DateTime? from, DateTime? to, string? table, int? userId)
    {
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value.Date);
        if (to.HasValue) q = q.Where(a => a.CreatedAt < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(table)) q = q.Where(a => a.TableName == table);
        if (userId.HasValue) q = q.Where(a => a.UserId == userId);
        return await q.OrderByDescending(a => a.CreatedAt).Take(200).Select(a => new AuditLogDto
        { Id = a.Id, UserId = a.UserId, UserName = a.User != null ? a.User.FullName : "System", AuditType = a.AuditType, TableName = a.TableName,
          PrimaryKey = a.PrimaryKey, OldValues = a.OldValues, NewValues = a.NewValues, AffectedColumns = a.AffectedColumns, CreatedAt = a.CreatedAt }).ToListAsync();
    }
    private static UserResponseDto Map(User u) => new() { Id = u.Id, FullName = u.FullName, UserName = u.UserName, Phone = u.Phone,
        Role = u.Role.ToString(), IsLocked = u.IsLocked, FailedLoginAttempts = u.FailedLoginAttempts, LastLoginAt = u.LastLoginAt, IsActive = u.IsActive, CreatedAt = u.CreatedAt };
}
