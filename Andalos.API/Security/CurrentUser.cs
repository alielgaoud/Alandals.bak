using Andalos.API.Data;
using Andalos.API.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Andalos.API.Security;

public sealed record UserSnapshot(int Id, string UserName, string FullName, UserRole Role, int? TenantId,
    string SecurityStamp, long PermissionsVersion, bool PermissionsReconciled, bool RequiresPasswordChange,
    bool IsActive, bool IsLocked, bool TenantActive)
{
    public bool IsStaff => Role is UserRole.SuperAdmin or UserRole.Admin or UserRole.Accountant or UserRole.GateKeeper;
    public bool IsTenant => (Role is UserRole.Tenant or UserRole.TenantStaff) && TenantId.HasValue && TenantActive;
    public bool IsSuperAdmin => IsStaff && Role == UserRole.SuperAdmin;
}

public sealed class CurrentUser
{
    public UserSnapshot? Snapshot { get; private set; }
    public UserSnapshot Required => Snapshot ?? throw new UnauthorizedAccessException("Authentication required.");
    public int UserId => Required.Id;
    public int TenantId => Required.IsTenant ? Required.TenantId!.Value : throw new ForbiddenOperationException();
    public void Set(UserSnapshot snapshot) => Snapshot = snapshot;
}

public sealed class UserSnapshotReader(AppDbContext db)
{
    public async Task<UserSnapshot?> ReadAsync(int id, CancellationToken ct = default)
    {
        SecurityMetrics.IdentityQueries.Add(1);
        return await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new UserSnapshot(u.Id, u.UserName, u.FullName, u.Role, u.TenantId,
                u.SecurityStamp, u.PermissionsVersion, u.PermissionsReconciled, u.RequiresPasswordChange,
                u.IsActive, u.IsLocked, u.TenantId == null || (u.Tenant != null && u.Tenant.IsActive)))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<bool> ValidateAsync(ClaimsPrincipal principal, CurrentUser current, CancellationToken ct = default)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) || id <= 0) return false;
        var user = await ReadAsync(id, ct);
        if (user is null || !user.IsActive || user.IsLocked || !Enum.IsDefined(user.Role) ||
            user.SecurityStamp.Length != 32 || !string.Equals(user.SecurityStamp, principal.FindFirstValue("security_stamp"), StringComparison.Ordinal) ||
            !string.Equals(user.Role.ToString(), principal.FindFirstValue(ClaimTypes.Role), StringComparison.Ordinal) ||
            (user.IsTenant && user.TenantId?.ToString() != principal.FindFirstValue("TenantId")) ||
            (!user.IsStaff && !user.IsTenant)) return false;
        current.Set(user);
        return true;
    }
}

public sealed class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException() : base("Access denied.") { }
}
public sealed class ConcurrencyConflictException(string message = "The resource changed; reload and retry.") : Exception(message);
