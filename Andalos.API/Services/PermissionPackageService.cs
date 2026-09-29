using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Users;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Andalos.API.Security;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services;
public sealed class PermissionPackageService(AppDbContext db, DelegationGuard guard, EffectivePermissions effective,
    UserSnapshotReader snapshots) : IPermissionPackageService
{
    public Task<List<PermissionModuleDto>> GetModulesAsync() => Task.FromResult(PermissionModules.ModuleMap.Keys
        .Select(m => new PermissionModuleDto { ModuleKey = m, ModuleName = PermissionModules.ModuleDisplayNames.GetValueOrDefault(m, m) })
        .OrderBy(m => m.ModuleKey).ToList());
    public async Task<List<PermissionPackageResponseDto>> GetAllAsync() => (await db.PermissionPackages
        .Include(p => p.Items).OrderBy(p => p.Name).ToListAsync()).Select(Map).ToList();
    public async Task<PermissionPackageResponseDto?> GetByIdAsync(int id)
    {
        var p = await db.PermissionPackages.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == id);
        return p is null ? null : Map(p);
    }
    public static List<string> ResolveKeys(bool keysSpecified, List<string>? keys, List<string>? modules, IEnumerable<string>? current = null)
    {
        if (modules is not null && modules.Any(m => !PermissionModules.ModuleMap.ContainsKey(m))) throw new ArgumentException("Unknown module.");
        // Supplied permissionKeys (including []) is authoritative. modules is classification metadata only in this case.
        if (keysSpecified) return Permissions.Validate(keys);
        if (modules is not null) return LegacyModuleExpansion.Expand(modules);
        return current?.ToList() ?? new List<string>(); // create empty; update omitted both preserves items.
    }
    public Task<PermissionPackageResponseDto> CreateAsync(CreatePermissionPackageDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin();
        var keys = ResolveKeys(dto.PermissionKeysSpecified, dto.PermissionKeys, dto.Modules);
        if (await db.PermissionPackages.AnyAsync(p => p.Name == dto.Name.Trim())) throw new ArgumentException("Package name already exists.");
        var p = new PermissionPackage { Name = dto.Name.Trim(), Description = dto.Description };
        foreach (var key in keys) p.Items.Add(new PermissionPackageItem { PermissionKey = key });
        db.PermissionPackages.Add(p);
        await db.SaveChangesAsync();
        return Map(p);
    });
    public Task<PermissionPackageResponseDto?> UpdateAsync(int id, UpdatePermissionPackageDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin();
        var p = await db.PermissionPackages.Include(p => p.Items).SingleOrDefaultAsync(p => p.Id == id);
        if (p is null) return null;
        if (dto.ExpectedRevision.HasValue && dto.ExpectedRevision != db.Entry(p).Property<Guid>("Revision").CurrentValue) throw new ConcurrencyConflictException();
        var keys = ResolveKeys(dto.PermissionKeysSpecified, dto.PermissionKeys, dto.Modules, p.Items.Where(i => i.IsActive).Select(i => i.PermissionKey));
        if (await db.PermissionPackages.AnyAsync(x => x.Id != id && x.Name == dto.Name.Trim())) throw new ArgumentException("Package name already exists.");
        p.Name = dto.Name.Trim(); p.Description = dto.Description; p.IsActive = dto.IsActive; p.UpdatedAt = DateTimeHelper.LibyaNow;
        foreach (var i in p.Items.ToArray())
        {
            if (!keys.Contains(i.PermissionKey, StringComparer.Ordinal)) { db.PermissionPackageItems.Remove(i); p.Items.Remove(i); }
            else i.IsActive = true;
        }
        foreach (var key in keys.Where(k => !p.Items.Any(i => i.PermissionKey == k))) p.Items.Add(new PermissionPackageItem { PackageId = id, PermissionKey = key });
        await db.SaveChangesAsync(); // version invalidation of ALL assignees in this same transaction.
        return Map(p);
    });
    public Task<bool> DeleteAsync(int id) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin();
        var p = await db.PermissionPackages.SingleOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (p is null) return false;
        p.IsActive = false; p.UpdatedAt = DateTimeHelper.LibyaNow;
        await db.SaveChangesAsync(); return true;
    });
    public Task<bool> AssignPackagesToUserAsync(AssignPackagesToUserDto dto) => db.AtomicAsync(async () =>
    {
        guard.RequireSuperAdmin(dto.UserId);
        var u = await db.Users.SingleOrDefaultAsync(u => u.Id == dto.UserId && u.IsActive);
        if (u is null) return false;
        DelegationGuard.RequireEmployeeTarget(u); DelegationGuard.CheckVersion(u, dto.ExpectedVersion);
        if (!u.PermissionsReconciled) throw new ArgumentException("Legacy permissions must be reconciled explicitly first.");
        await ReplaceAssignmentsAsync(u.Id, dto.PackageIds);
        await db.SaveChangesAsync(); return true;
    });
    public async Task ReplaceAssignmentsAsync(int userId, List<int>? packageIds)
    {
        if (packageIds is null || packageIds.Count > 100) throw new ArgumentException("packageIds must be an array of at most 100 IDs.");
        var ids = packageIds.Distinct().Order().ToArray();
        var valid = await db.PermissionPackages.Where(p => ids.Contains(p.Id) && p.IsActive).Select(p => p.Id).ToListAsync();
        if (valid.Count != ids.Length) throw new ArgumentException("Unknown or inactive package ID.");
        var old = await db.UserPermissionPackages.Where(x => x.UserId == userId).ToListAsync();
        foreach (var link in old)
            if (!ids.Contains(link.PackageId)) db.UserPermissionPackages.Remove(link);
            else link.IsActive = true;
        foreach (var id in ids.Where(id => !old.Any(x => x.PackageId == id))) db.UserPermissionPackages.Add(new() { UserId = userId, PackageId = id });
    }
    public async Task<List<PermissionPackageResponseDto>> GetUserPackagesAsync(int userId) =>
        (await db.UserPermissionPackages.Where(x => x.UserId == userId && x.IsActive && x.User!.IsActive && x.Package!.IsActive)
            .Select(x => x.Package!).Include(p => p.Items).Distinct().ToListAsync()).Select(Map).ToList();
    public async Task<List<string>> GetEffectivePermissionsForUserAsync(int userId)
    {
        var user = await snapshots.ReadAsync(userId);
        return user is null ? new() : (await effective.GetAsync(user)).ToList();
    }
    private PermissionPackageResponseDto Map(PermissionPackage p)
    {
        var keys = p.Items.Where(i => i.IsActive).Select(i => i.PermissionKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var modules = Permissions.ModulesFor(keys);
        return new() { Id = p.Id, Name = p.Name, Description = p.Description, IsActive = p.IsActive,
            PermissionKeys = keys, Modules = modules, ModuleNames = modules.Select(m => PermissionModules.ModuleDisplayNames.GetValueOrDefault(m, m)).ToList(),
            Revision = db.Entry(p).Property<Guid>("Revision").CurrentValue };
    }
}
