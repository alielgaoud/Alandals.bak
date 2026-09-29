using Microsoft.EntityFrameworkCore.Infrastructure;
using Andalos.API.Data;
using Andalos.API.Models;
using Andalos.API.Enums;
using Andalos.API.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Andalos.API.Tests;
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANDALOS_TEST_SQL"))) Skip = "Requires disposable SQL Server; see docs/authorization/VERIFICATION.ar.md"; }
}
public sealed class SqlServerTests
{
    private static ApiFactory Factory()
    {
        var cs = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ANDALOS_TEST_SQL"))
        { InitialCatalog = "AndalosAuthorizationTests_" + Guid.NewGuid().ToString("N") };
        return new ApiFactory(cs.ConnectionString);
    }
    private static HttpRequestMessage TopUp(string code, decimal amount, string key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/VisitorWallet/gate/add-balance") { Content = JsonContent.Create(new { passCode = code, amount }) };
        req.Headers.Add("Idempotency-Key", key); return req;
    }
    [SqlServerFact]
    public async Task AllMigrationsApplyAndFrozenSnapshotMatchesCurrentModel()
    {
        using var factory = Factory();
        try
        {
            await factory.InitializeDatabaseAsync(migrate: true);
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Contains("20260929190000_DetailedAuthorization", await db.Database.GetAppliedMigrationsAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); }
    }
    [SqlServerFact]
    public async Task UpgradePreservesMixedLegacyRowsButDoesNotInventDirectGrantsOrHistoricalCash()
    {
        using var factory = Factory();
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.GetService<IMigrator>().MigrateAsync("20260926230000_AddChargeApprovalWorkflow");
                await db.Database.ExecuteSqlRawAsync("""
                  INSERT INTO Users(FullName,UserName,PasswordHash,Role,IsLocked,FailedLoginAttempts,CreatedAt,IsActive)
                  VALUES (N'Legacy',N'legacy-test',N'not-a-secret',2,0,0,GETUTCDATE(),1);
                  DECLARE @uid int=SCOPE_IDENTITY();
                  INSERT INTO UserPermissions(UserId,PermissionKey,CreatedAt,IsActive) VALUES(@uid,N'Units.View',GETUTCDATE(),1);
                  """);
                await db.Database.MigrateAsync();
                var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserName == "legacy-test");
                Assert.False(user.PermissionsReconciled); Assert.Equal(32, user.SecurityStamp.Length);
                Assert.True(await db.UserPermissions.AnyAsync(p => p.UserId == user.Id)); Assert.False(await db.DirectUserPermissions.AnyAsync(p => p.UserId == user.Id));
                Assert.Empty(await db.GateCashReceipts.ToListAsync());
            }
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); }
    }
    [SqlServerFact]
    public async Task ConcurrentSameKeyTopupCreditsExactlyOnceAndChangedRequestIs409()
    {
        using var factory = Factory();
        try
        {
            await factory.InitializeDatabaseAsync(); var user = await factory.EmployeeAsync(UserRole.GateKeeper, "VisitorWallet.AddBalanceToPass");
            await factory.ChangeAsync(db => db.VisitorPasses.Add(new() { PassCode = "TEST-TOPUP", VisitorName = "Test", ValidDate = Andalos.API.Helpers.DateTimeHelper.LibyaToday,
                IsPaidPass = true, RemainingBalance = 10, InitialBalance = 10, IssuedByUserId = user.Id }));
            using var client = factory.Client(user);
            var requests = Enumerable.Range(0, 8).Select(_ => client.SendAsync(TopUp("TEST-TOPUP", 20, "same-key-12345"))).ToArray();
            var responses = await Task.WhenAll(requests);
            Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
            Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(TopUp("TEST-TOPUP", 20, "same-key-12345"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(TopUp("TEST-TOPUP", 21, "same-key-12345"))).StatusCode);
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(30m, (await db.VisitorPasses.SingleAsync()).RemainingBalance);
            Assert.Single(await db.GateCashReceipts.ToListAsync()); Assert.Single(await db.IdempotencyRecords.ToListAsync());
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); }
    }
    [SqlServerFact]
    public async Task BlacklistedPassCannotBeToppedUpAndInvalidAmountsLeaveNoEffects()
    {
        using var factory = Factory();
        try
        {
            await factory.InitializeDatabaseAsync(); var user = await factory.EmployeeAsync(UserRole.GateKeeper, "VisitorWallet.AddBalanceToPass");
            await factory.ChangeAsync(db => { db.VisitorPasses.Add(new() { PassCode = "BLACKLISTED", VisitorName = "Test", VisitorPhone = "0999999999",
                ValidDate = Andalos.API.Helpers.DateTimeHelper.LibyaToday, IsPaidPass = true, RemainingBalance = 10, InitialBalance = 10 });
                db.VisitorBlacklists.Add(new() { FullName = "Test", Phone = "0999999999", Reason = "test" }); });
            using var c = factory.Client(user);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(TopUp("BLACKLISTED", 20, "blacklist-key"))).StatusCode);
            foreach (var amount in new[] { -1m, 0m, 0.001m, 1000000m })
                Assert.Equal(HttpStatusCode.BadRequest, (await c.SendAsync(TopUp("BLACKLISTED", amount, Guid.NewGuid().ToString("N")))).StatusCode);
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(10m, (await db.VisitorPasses.SingleAsync()).RemainingBalance); Assert.Empty(await db.GateCashReceipts.ToListAsync());
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); }
    }
    [SqlServerFact]
    public async Task ConcurrentDirectAndPackageReplacementWithSameWitnessCannotBothCommit()
    {
        using var factory = Factory();
        try
        {
            await factory.InitializeDatabaseAsync(); var root = await factory.EmployeeAsync(UserRole.SuperAdmin); var target = await factory.EmployeeAsync(UserRole.Admin);
            int packageId = 0;
            await factory.ChangeAsync(db => { var p = new PermissionPackage { Name = "concurrent", Items = new List<PermissionPackageItem> { new() { PermissionKey = "Units.View" } } };
                db.PermissionPackages.Add(p); db.SaveChanges(); packageId = p.Id; });
            using var c = factory.Client(root);
            var results = await Task.WhenAll(
                c.PutAsJsonAsync($"/api/Users/{target.Id}/permissions", new { userId = target.Id, permissions = new[] { "Units.View" }, expectedVersion = target.PermissionsVersion }),
                c.PostAsJsonAsync("/api/PermissionPackages/assign-to-user", new { userId = target.Id, packageIds = new[] { packageId }, expectedVersion = target.PermissionsVersion }));
            Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.OK)); Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Conflict));
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(target.PermissionsVersion + 1, (await db.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id)).PermissionsVersion);
            Assert.Equal(1, await db.DirectUserPermissions.CountAsync(x => x.UserId == target.Id) + await db.UserPermissionPackages.CountAsync(x => x.UserId == target.Id));
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); }
    }
    [SqlServerFact]
    public async Task WarmPermissionCacheNeverAllowsAccessWhenIdentityDatabaseIsUnavailable()
    {
        using var factory = Factory();
        try
        {
            await factory.InitializeDatabaseAsync(); var user = await factory.EmployeeAsync(UserRole.Admin, "Units.View"); using var c = factory.Client(user);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/Units")).StatusCode);
            using (var scope = factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.GetAsync("/api/Units")).StatusCode);
        }
        finally { using var scope = factory.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync(); }
    }
}
