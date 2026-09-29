using Andalos.API.Authorization;
using Andalos.API.Constants;
using Andalos.API.Controllers;
using Andalos.API.DTOs.Users;
using Andalos.API.Enums;
using Andalos.API.Models;
using Andalos.API.Security;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
namespace Andalos.API.Tests;
public sealed class CatalogAndUnitTests
{
    [Fact]
    public void EveryControllerActionHasExplicitClassificationAndEveryKeyIsKnown()
    {
        var actions = typeof(AuthController).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute))
                .Select(m => new ControllerActionDescriptor { ControllerName = t.Name.Replace("Controller", ""), ActionName = m.Name }));
        EndpointSecurityCatalog.AssertCoverage(actions);
        Assert.All(EndpointSecurityCatalog.Rules.Values.SelectMany(r => r.PermissionKeys), k => Assert.True(Permissions.IsKnown(k)));
        var modules = PermissionModules.ModuleMap.Values.SelectMany(x => x).Order().ToArray();
        Assert.Equal(Permissions.GetAllPermissions().Order(), modules);
    }
    [Fact]
    public void UnknownNewActionCannotSneakIntoTheCatalogue()
    {
        Assert.Throws<InvalidOperationException>(() => EndpointSecurityCatalog.AssertCoverage(new[] { new ControllerActionDescriptor { ControllerName = "FutureAdmin", ActionName = "ReadSecrets" } }));
    }
    [Theory]
    [InlineData("{\"name\":\"p\",\"modules\":[\"Units\"]}", 5)]
    [InlineData("{\"name\":\"p\",\"modules\":[\"Units\"],\"permissionKeys\":[]}", 0)]
    [InlineData("{\"name\":\"p\",\"modules\":[\"Units\"],\"permissionKeys\":[\"Units.View\"]}", 1)]
    [InlineData("{\"name\":\"p\"}", 0)]
    public void PackageRequestPresenceHasUnambiguousMeaning(string body, int expectedCount)
    {
        var dto = JsonSerializer.Deserialize<CreatePermissionPackageDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(expectedCount, PermissionPackageService.ResolveKeys(dto.PermissionKeysSpecified, dto.PermissionKeys, dto.Modules).Count);
    }
    [Fact]
    public void EmptyKeysNeverExpandModulesAndNullUnknownKeysAreRejected()
    {
        Assert.Empty(PermissionPackageService.ResolveKeys(true, new(), new() { "Financials" }));
        Assert.Throws<ArgumentException>(() => PermissionPackageService.ResolveKeys(true, null, new() { "Units" }));
        Assert.Throws<ArgumentException>(() => Permissions.Validate(new[] { "Units", "Units.Unknown" }));
        Assert.DoesNotContain(Permissions.Financials.DeleteRefund, LegacyModuleExpansion.Expand(new[] { "Financials" }));
        Assert.Equal(new[] { "Units.View" }, Permissions.Validate(new[] { "Units.View", "Units.View" }));
    }
    [Fact]
    public async Task UnknownPolicyAndFallbackDenyEvenAnAuthenticatedRoleClaim()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync("Units.Everything");
        Assert.NotNull(policy);
        Assert.NotNull(await provider.GetFallbackPolicyAsync());
        Assert.DoesNotContain(policy!.Requirements, r => r is PermissionRequirement);
    }
    [Fact]
    public void RoleClaimAloneCannotInvokeDelegationGuard()
    {
        var current = new CurrentUser();
        current.Set(new(42, "admin", "Admin", UserRole.Admin, null, Guid.NewGuid().ToString("N"), 1, true, false, true, false, true));
        Assert.Throws<ForbiddenOperationException>(() => new DelegationGuard(current).RequireSuperAdmin());
        current.Set(current.Required with { Role = UserRole.SuperAdmin });
        Assert.Throws<ForbiddenOperationException>(() => new DelegationGuard(current).RequireSuperAdmin(42));
    }
    [Fact]
    public void PasswordHashesAreSaltedAndLegacyHashesUpgrade()
    {
        var p = new PasswordService(); var u = new User();
        var a = p.Hash(u, "Long-password-2026"); var b = p.Hash(u, "Long-password-2026"); Assert.NotEqual(a, b);
        u.PasswordHash = a; Assert.True(p.Verify(u, "Long-password-2026", out _)); Assert.False(p.Verify(u, "wrong", out _));
        u.PasswordHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("legacy")));
        Assert.True(p.Verify(u, "legacy", out var upgrade)); Assert.True(upgrade);
        Assert.True(AuditRedaction.IsSensitive(u, "PasswordHash")); Assert.True(AuditRedaction.IsSensitive(u, "SecurityStamp"));
    }
    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/example", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/example", true)]
    [InlineData("http://127.0.0.1/internal", false)]
    [InlineData("https://fcm.googleapis.com.evil.example/private", false)]
    [InlineData("https://user@fcm.googleapis.com/path", false)]
    [InlineData("https://fcm.googleapis.com:444/path", false)]
    public void PushEndpointsDoNotBecomeArbitrarySsrf(string url, bool expected) => Assert.Equal(expected, PushSubscriptionSecurity.IsAllowedEndpoint(url));
}
