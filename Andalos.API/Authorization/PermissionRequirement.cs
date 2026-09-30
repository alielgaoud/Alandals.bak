using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Andalos.API.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
        }
    }

    public class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public PermissionAuthorizationHandler(
            IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return;

            var rawId = context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(rawId, out var userId))
                return;

            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var account = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Id == userId && u.IsActive);

            if (account == null ||
                account.IsLocked ||
                !context.User.IsInRole(account.Role.ToString()))
            {
                return;
            }

            // التجاوز يعتمد الدور الحالي في قاعدة البيانات،
            // وليس دوراً قديماً داخل التوكن.
            if (account.Role == UserRole.SuperAdmin)
            {
                context.Succeed(requirement);
                return;
            }

            var packages = scope.ServiceProvider
                .GetRequiredService<IPermissionPackageService>();

            var effective = await packages
                .GetEffectivePermissionsForUserAsync(userId);

            if (effective.Contains(
                    requirement.Permission,
                    StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
        }
    }

    public class PermissionPolicyProvider
        : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallback;

        public PermissionPolicyProvider(
            IOptions<AuthorizationOptions> options)
        {
            _fallback =
                new DefaultAuthorizationPolicyProvider(options);
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
            => _fallback.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
            => _fallback.GetFallbackPolicyAsync();

        public async Task<AuthorizationPolicy?> GetPolicyAsync(
            string policyName)
        {
            // لا تحوّل السياسات المسماة الأخرى تلقائياً
            // إلى متطلبات صلاحية.
            var configured = await _fallback
                .GetPolicyAsync(policyName);

            if (configured != null)
                return configured;

            if (!Regex.IsMatch(
                    policyName,
                    @"^[A-Za-z]+\.[A-Za-z]+$"))
            {
                return null;
            }

            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(
                    new PermissionRequirement(policyName))
                .Build();
        }
    }
}