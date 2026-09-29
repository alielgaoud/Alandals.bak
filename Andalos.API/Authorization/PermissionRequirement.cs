using Andalos.API.Constants;
using Andalos.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Andalos.API.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler(CurrentUser current, EffectivePermissions permissions)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            // Only the live, stamp-validated identity set by JWT authentication can authorize.
            var user = current.Snapshot;
            if (context.User.Identity?.IsAuthenticated != true || user is null || !user.IsStaff ||
                !user.IsActive || user.IsLocked || user.RequiresPasswordChange || !Permissions.IsKnown(requirement.Permission)) return;
            if (await permissions.HasAsync(user, requirement.Permission)) context.Succeed(requirement);
        }
        finally { SecurityMetrics.Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds); }
    }
}

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _default = new(options);
    private static readonly AuthorizationPolicy Deny = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().RequireAssertion(_ => false).Build();
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _default.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(Deny);
    public async Task<AuthorizationPolicy?> GetPolicyAsync(string name)
    {
        if (Permissions.IsKnown(name)) return new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(name)).Build();
        return await _default.GetPolicyAsync(name) ?? Deny; // Unknown strings fail closed.
    }
}
