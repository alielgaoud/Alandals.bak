using Microsoft.AspNetCore.Authorization;
namespace Andalos.API.Security;
public static class IdentityPolicies
{
    public const string Authenticated = "Identity.Authenticated";
    public const string Staff = "Identity.Staff";
    public const string Tenant = "Identity.Tenant";
    public const string TenantOwner = "Identity.TenantOwner";
    public const string Self = "Identity.Self";
    public const string File = "Identity.ProtectedFile";
    public const string Denied = "Identity.Unclassified";
}
public sealed record IdentityRequirement(string Scope, string? Capability = null) : IAuthorizationRequirement;
public sealed class IdentityAuthorizationHandler(CurrentUser current, Andalos.API.Data.AppDbContext db)
    : AuthorizationHandler<IdentityRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, IdentityRequirement requirement)
    {
        var user = current.Snapshot;
        if (context.User.Identity?.IsAuthenticated != true || user is null || !user.IsActive || user.IsLocked) return;
        if (user.RequiresPasswordChange && requirement.Scope != IdentityPolicies.Authenticated) return;
        var ok = requirement.Scope switch
        {
            IdentityPolicies.Authenticated => true,
            IdentityPolicies.Staff => user.IsStaff,
            IdentityPolicies.Self => true,
            IdentityPolicies.File => true, // Resource-dependent checks in ProtectedFilesController.
            IdentityPolicies.Tenant => user.IsTenant,
            IdentityPolicies.TenantOwner => user.IsTenant && user.Role == Andalos.API.Enums.UserRole.Tenant,
            _ => false
        };
        if (ok && requirement.Capability is not null && user.Role == Andalos.API.Enums.UserRole.TenantStaff)
        {
            // Portal scope is distinct from employee permissions; no wildcard/module expansion.
            ok = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.TenantStaffPermissions,
                p => p.UserId == user.Id && p.Capability == requirement.Capability);
        }
        if (ok) context.Succeed(requirement);
    }
}
