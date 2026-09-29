using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Andalos.API.Security;

// Only immutable permission unions are cached; identity and version are NEVER cached.
// A database version bump invalidates this cache across every replica without a pub/sub race.
public sealed class PermissionUnionCache : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 4096 });
    public bool TryGet(int userId, long version, out string[] keys) =>
        _cache.TryGetValue((userId, version), out keys!);
    public void Set(int userId, long version, string[] keys) => _cache.Set((userId, version), keys,
        new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5), Size = 1 });
    public void Dispose() => _cache.Dispose();
}

public sealed class EffectivePermissions(AppDbContext db, PermissionUnionCache cache)
{
    public async Task<string[]> GetAsync(UserSnapshot user, CancellationToken ct = default)
    {
        if (!user.IsActive || user.IsLocked || user.RequiresPasswordChange || !user.IsStaff) return Array.Empty<string>();
        if (user.IsSuperAdmin) return Permissions.GetAllPermissions().ToArray();
        if (!user.PermissionsReconciled) return Array.Empty<string>();
        if (cache.TryGet(user.Id, user.PermissionsVersion, out var keys))
        {
            SecurityMetrics.CacheHits.Add(1);
            return keys;
        }
        SecurityMetrics.CacheMisses.Add(1);
        // An exact UNION translated to one SQL statement. Every layer's activity is checked.
        var direct = db.DirectUserPermissions.Where(p => p.UserId == user.Id && p.IsActive && p.User!.IsActive)
            .Select(p => p.PermissionKey);
        var packages = db.UserPermissionPackages.Where(x => x.UserId == user.Id && x.IsActive && x.User!.IsActive &&
                x.Package != null && x.Package.IsActive)
            .SelectMany(x => x.Package!.Items.Where(i => i.IsActive).Select(i => i.PermissionKey));
        SecurityMetrics.UnionQueries.Add(1);
        keys = (await direct.Union(packages).ToListAsync(ct)).Where(Permissions.IsKnown)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        // Never poison a *new* version with a union fetched across a concurrent mutation.
        // The key is the old witness version, so the next request uses the new key.
        cache.Set(user.Id, user.PermissionsVersion, keys);
        return keys;
    }
    public async Task<bool> HasAsync(UserSnapshot user, string key, CancellationToken ct = default) =>
        Permissions.IsKnown(key) && (await GetAsync(user, ct)).Contains(key, StringComparer.Ordinal);
}
