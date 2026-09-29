using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Users;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Andalos.API.Security;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Andalos.API.Tests;
public sealed class HttpAuthorizationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();
    public Task DisposeAsync() { _factory.Dispose(); return Task.CompletedTask; }
    private static HttpRequestMessage Command(HttpMethod method, string route, object? body = null, string? key = null)
    {
        var r = new HttpRequestMessage(method, route) { Content = JsonContent.Create(body ?? new { }) };
        r.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N")); return r;
    }
    private static async Task<string[]> KeysAsync(HttpClient client)
    {
        var r = await client.GetAsync("/api/Auth/me/permissions"); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var json = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("permissions").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }
    [Fact]
    public async Task AdminWithOnlyUnitsViewCannotWriteByDirectHttp()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Units.View); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/Units")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/Units", new { unitNumber = "forbidden" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync("/api/Units/1", new { area = 12 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/Units/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Units/1/history")).StatusCode);
    }
    [Theory]
    [InlineData(UserRole.Admin)] [InlineData(UserRole.Accountant)] [InlineData(UserRole.GateKeeper)]
    public async Task RoleAndModuleNamesGiveNoEmployeeGrants(UserRole role)
    {
        var u = await _factory.EmployeeAsync(role, "Units"); using var c = _factory.Client(u);
        Assert.Empty(await KeysAsync(c)); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Units")).StatusCode);
    }
    [Fact]
    public async Task AnonymousIs401AndAuthenticatedMissingPermissionIs403()
    {
        using var anonymous = _factory.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/Units")).StatusCode);
        var u = await _factory.EmployeeAsync(UserRole.Admin); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Units")).StatusCode);
    }
    [Fact]
    public async Task LoginAlwaysIncludesActualPermissionArraysAndUtcExpiration()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin); using var c = _factory.Client();
        var r = await c.PostAsJsonAsync("/api/Auth/login", new { userName = u.UserName, password = "A-long-test-password-2026" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        var data = j.RootElement.GetProperty("data"); Assert.Equal(0, data.GetProperty("permissions").GetArrayLength());
        Assert.Equal(0, data.GetProperty("modules").GetArrayLength());
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(data.GetProperty("token").GetString());
        Assert.Equal(token.ValidTo, data.GetProperty("expiration").GetDateTime().ToUniversalTime(), TimeSpan.FromSeconds(1));
    }
    [Fact]
    public async Task VisitorsViewDoesNotAllowCreationScanOrGateLogs()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Visitors.View); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/VisitorPasses")).StatusCode);
        foreach (var path in new[] { "/api/Gate/scan", "/api/VisitorPasses", "/api/VisitorWallet/issue-paid-pass" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(Command(HttpMethod.Post, path))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Gate/logs")).StatusCode);
    }
    [Theory]
    [InlineData("VisitorWallet.SettleShopBalance", "/api/VisitorWallet/gate/add-balance")]
    [InlineData("VisitorWallet.AddBalanceToPass", "/api/VisitorWallet/admin/settle-shop")]
    public async Task SettlementAndTopupKeysAreIndependent(string key, string deniedRoute)
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, key); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(Command(HttpMethod.Post, deniedRoute))).StatusCode);
    }
    [Fact]
    public async Task PaymentReaderCannotCreateCancelRefundReviewOrReadBalances()
    {
        var u = await _factory.EmployeeAsync(UserRole.Accountant, Permissions.Financials.ViewPayments); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/Payments")).StatusCode);
        foreach (var path in new[] { "/api/Payments", "/api/Refunds", "/api/BankTransfers/1/review" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(Command(HttpMethod.Post, path))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/Payments/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Payments/summary")).StatusCode);
    }
    [Fact]
    public async Task PaymentCancellationNeverImpliesRefundCancellation()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Financials.DeletePayment); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(Command(HttpMethod.Delete, "/api/Refunds/1"))).StatusCode);
    }
    [Fact]
    public async Task TenantViewDoesNotExposeNestedFinanceContractsOrVisitors()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Tenants.View); using var c = _factory.Client(u);
        foreach (var segment in new[] { "units", "contracts", "payments", "maintenance", "visitor-passes", "financial-summary" })
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/Tenants/1/{segment}")).StatusCode);
    }
    [Fact]
    public async Task DashboardKeyAloneCannotDiscloseFinancialData()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Reports.ViewDashboard); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Reports/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Reports/financial-performance/2026")).StatusCode);
    }
    [Fact]
    public async Task PackageWithdrawalAndDeactivationPreserveIndependentSourcesAndInvalidateOldSession()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Units.View);
        int first = 0, second = 0;
        await _factory.ChangeAsync(db =>
        {
            var p1 = new PermissionPackage { Name = "one", Items = new List<PermissionPackageItem> { new() { PermissionKey = Permissions.Units.ViewHistory }, new() { PermissionKey = Permissions.Units.View } } };
            var p2 = new PermissionPackage { Name = "two", Items = new List<PermissionPackageItem> { new() { PermissionKey = Permissions.Units.ViewHistory }, new() { PermissionKey = Permissions.Units.View } } };
            db.PermissionPackages.AddRange(p1, p2); db.SaveChanges(); first = p1.Id; second = p2.Id;
            db.UserPermissionPackages.AddRange(new() { UserId = u.Id, PackageId = first }, new() { UserId = u.Id, PackageId = second });
        });
        using var c = _factory.Client(u); Assert.Equal(2, (await KeysAsync(c)).Length); // fills permission cache
        await _factory.ChangeAsync(db => db.UserPermissionPackages.Remove(db.UserPermissionPackages.Single(x => x.UserId == u.Id && x.PackageId == first)));
        Assert.Equal(2, (await KeysAsync(c)).Length);
        await _factory.ChangeAsync(db => db.PermissionPackages.Find(second)!.IsActive = false);
        Assert.Equal(new[] { Permissions.Units.View }, await KeysAsync(c));
        await _factory.ChangeAsync(db => db.DirectUserPermissions.Remove(db.DirectUserPermissions.Single(x => x.UserId == u.Id)));
        Assert.Empty(await KeysAsync(c)); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Units")).StatusCode);
    }
    [Fact]
    public async Task InactivePackageLinkAndItemNeverGrant()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin);
        await _factory.ChangeAsync(db =>
        {
            var p = new PermissionPackage { Name = "inactive", Items = new List<PermissionPackageItem> { new() { PermissionKey = Permissions.Units.View, IsActive = false } } };
            db.PermissionPackages.Add(p); db.SaveChanges(); db.UserPermissionPackages.Add(new() { UserId = u.Id, PackageId = p.Id, IsActive = false });
        });
        using var c = _factory.Client(u); Assert.Empty(await KeysAsync(c));
    }
    [Fact]
    public async Task AdminCannotPromoteSelfManageOtherUsersOrCreateEscalatingPackage()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Users.ManagePermissions, Permissions.Users.Edit); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/Users/{u.Id}", new { fullName = "x", userName = u.UserName, role = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/Users/{u.Id}/permissions", new { userId = u.Id, permissions = new[] { Permissions.Units.Delete } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/PermissionPackages", new { name = "escalate", permissionKeys = new[] { Permissions.Users.ManagePermissions } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/PermissionPackages/assign-to-user", new { userId = u.Id, packageIds = new[] { 1 } })).StatusCode);
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public async Task PublicRoleBearingRegistrationIsDisabled(int role)
    {
        using var c = _factory.Client(); var r = await c.PostAsJsonAsync("/api/Auth/register", new
        { fullName = "attacker", userName = "attacker", password = "A-long-test-password-2026", confirmPassword = "A-long-test-password-2026", role });
        Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
    }
    [Fact]
    public async Task SuperAdminCannotEditOwnRoleOrPermissionsAndRejectsUnknownKeys()
    {
        var root = await _factory.EmployeeAsync(UserRole.SuperAdmin); var target = await _factory.EmployeeAsync(UserRole.Admin); using var c = _factory.Client(root);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"/api/Users/{root.Id}/permissions", new { userId = root.Id, permissions = new[] { "Units.View" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/Users/{target.Id}/permissions", new { userId = target.Id, permissions = new[] { "Units.NotReal" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/Users/{target.Id}/permissions", new { userId = target.Id, permissions = Array.Empty<string>() })).StatusCode);
    }
    [Fact]
    public async Task PermanentLockRejectsExistingTokenAndCannotBeUndoneByLogin()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin, Permissions.Units.View); using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/Units")).StatusCode);
        await _factory.ChangeAsync(db => { var entity = db.Users.Find(u.Id)!; entity.IsLocked = true; entity.LockoutEnd = null; });
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/Units")).StatusCode);
        using var login = _factory.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await login.PostAsJsonAsync("/api/Auth/login", new { userName = u.UserName, password = "A-long-test-password-2026" })).StatusCode);
    }
    [Fact]
    public async Task ARoleClaimNotMatchingTheLiveDatabaseCannotBypassPermissions()
    {
        var u = await _factory.EmployeeAsync(UserRole.Admin); u.Role = UserRole.SuperAdmin; using var c = _factory.Client(u);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/Units")).StatusCode);
    }
    [Fact]
    public async Task TenantCannotChangeIdReadOthersFilesImpersonateHeadersOrReceiveEmployeePermissions()
    {
        var a = await _factory.TenantAsync(); var b = await _factory.TenantAsync();
        await _factory.ChangeAsync(db =>
        {
            db.DirectUserPermissions.Add(new() { UserId = a.Id, PermissionKey = Permissions.Units.View });
            db.ProtectedDocuments.Add(new() { Path = "/uploads/demands/private.pdf", TenantId = b.TenantId!.Value });
        });
        using var c = _factory.Client(a);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/tenant-complaints/{b.TenantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/TenantPortal/statement/{b.TenantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/uploads/demands/private.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Units")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Auth/me/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/Notifications/summary?testUserId={b.Id}")).StatusCode);
        c.DefaultRequestHeaders.Add("X-Test-Tenant-Id", b.TenantId!.Value.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/Notifications/summary")).StatusCode);
        using var login = _factory.Client(); var r = await login.PostAsJsonAsync("/api/Auth/tenant-login", new { userName = a.UserName, password = "A-long-test-password-2026" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); using var j = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        Assert.False(j.RootElement.GetProperty("data").TryGetProperty("permissions", out _));
    }
    [Fact]
    public async Task TenantStaffCannotAcquireEmployeeGrantsOrCreateMoreStaff()
    {
        var staff = await _factory.TenantAsync(true); using var c = _factory.Client(staff);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/api/TenantPortal/contracts/{staff.TenantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync($"/api/TenantPortal/staff/{staff.TenantId}", new { })).StatusCode);
    }
    [Fact]
    public async Task DifferentTenantChargeIdIsRejectedDespiteCorrectTenantIdInUrl()
    {
        var a = await _factory.TenantAsync(); var b = await _factory.TenantAsync(); int id = 0;
        await _factory.ChangeAsync(db => { var charge = new TenantCharge { ChargeNumber = "FOREIGN", TenantId = b.TenantId!.Value, Amount = 10, Description = "foreign" }; db.TenantCharges.Add(charge); db.SaveChanges(); id = charge.Id; });
        using var c = _factory.Client(a);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(Command(HttpMethod.Post, $"/api/TenantPortal/maintenance/{a.TenantId}/charges/{id}/accept"))).StatusCode);
    }
    [Fact]
    public void RuntimeMvcDescriptorsExactlyMatchSecurityCatalogue()
    {
        var descriptors = _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();
        EndpointSecurityCatalog.AssertCoverage(descriptors);
        Assert.All(descriptors, d => Assert.Contains(d.EndpointMetadata, m => m is EndpointSecurityRule));
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "authorization-endpoints.json")));
        var published = inventory.RootElement.EnumerateArray().ToDictionary(r => r.GetProperty("action").GetString()!);
        Assert.Equal(descriptors.Length, published.Count);
        foreach (var action in descriptors)
        {
            var row = published[EndpointSecurityCatalog.Key(action)];
            Assert.Equal(row.GetProperty("route").GetString()!.TrimStart('/'), action.AttributeRouteInfo!.Template.TrimStart('/'));
            var methods = action.ActionConstraints!.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods);
            Assert.Contains(row.GetProperty("http").GetString()!, methods);
        }
    }
    [Fact]
    public async Task PasswordsAndStampsAreNeverWrittenIntoAuditValues()
    {
        await _factory.EmployeeAsync(UserRole.Admin);
        using var scope = _factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AuditLogs.ToListAsync(); Assert.NotEmpty(logs);
        Assert.All(logs, a => { Assert.DoesNotContain("PasswordHash", (a.OldValues ?? "") + (a.NewValues ?? "")); Assert.DoesNotContain("SecurityStamp", (a.OldValues ?? "") + (a.NewValues ?? "")); });
    }
    [Fact]
    public async Task MalformedAllTenantsAndForeignDirectNotificationRowsDoNotLeak()
    {
        var a = await _factory.TenantAsync(); var b = await _factory.TenantAsync();
        await _factory.ChangeAsync(db => db.Notifications.AddRange(
            new() { Title = "foreign group", Message = "private", Type = NotificationType.General, TenantId = b.TenantId, TargetGroup = "AllTenants", IsSent = true },
            new() { Title = "foreign direct", Message = "private", Type = NotificationType.General, UserId = a.Id, TenantId = b.TenantId, IsSent = true },
            new() { Title = "public tenant broadcast", Message = "public", Type = NotificationType.General, TargetGroup = "AllTenants", IsSent = true }));
        using var c = _factory.Client(a); var response = await c.GetAsync("/api/Notifications"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("private", content); Assert.Contains("public tenant broadcast", content);
    }
}
