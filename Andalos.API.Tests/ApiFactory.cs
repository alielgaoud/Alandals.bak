using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Models;
using Andalos.API.Security;
using Andalos.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;

namespace Andalos.API.Tests;
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    static ApiFactory()
    {
        // Needed before minimal-host Program reads secrets; scoped to this ephemeral test process.
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", "ONLY-EPHEMERAL-TESTS-NOT-PRODUCTION-KEY-64-CHARACTERS-2026");
    }
    private readonly SqliteConnection? _connection;
    private readonly string? _sql;
    public ApiFactory(string? sql = null)
    {
        _sql = sql;
        if (sql is null) { _connection = new SqliteConnection("DataSource=:memory:"); _connection.Open(); }
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "ONLY-EPHEMERAL-TESTS-NOT-PRODUCTION-KEY-64-CHARACTERS-2026", ["JwtSettings:ExpiryMinutes"] = "60",
            ["JwtSettings:Issuer"] = "AndalosAPI", ["JwtSettings:Audience"] = "AndalosClients", ["Database:InitializeOnStartup"] = "false",
            ["ConnectionStrings:DefaultConnection"] = _sql ?? "Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true"
        }));
        builder.ConfigureTestServices(services =>
        {
            // No scheduler or outbox network activity during acceptance tests; keep the coverage startup check.
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IHostedService) &&
                (d.ImplementationType == typeof(SystemSchedulerService) || d.ImplementationType == typeof(NotificationDispatcher))).ToArray()) services.Remove(descriptor);
            if (_sql is null)
            {
                // Remove all pooled SQL Server registrations, including EF's options configuration callbacks.
                foreach (var d in services.Where(d => d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) || d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType.FullName?.Contains("DbContextPool", StringComparison.Ordinal) == true ||
                    d.ServiceType.FullName?.Contains("ScopedDbContextLease", StringComparison.Ordinal) == true ||
                    d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration", StringComparison.Ordinal) == true).ToArray()) services.Remove(d);
                services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection!));
            }
        });
    }
    public async Task InitializeDatabaseAsync(bool migrate = false)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (migrate) await db.Database.MigrateAsync(); else await db.Database.EnsureCreatedAsync();
    }
    public async Task<User> EmployeeAsync(UserRole role, params string[] keys)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { FullName = "Test Employee", UserName = "test-" + Guid.NewGuid().ToString("N"), Role = role };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordService>().Hash(user, "A-long-test-password-2026");
        db.Users.Add(user); await db.SaveChangesAsync();
        foreach (var key in keys.Distinct()) db.DirectUserPermissions.Add(new() { UserId = user.Id, PermissionKey = key });
        await db.SaveChangesAsync();
        return user;
    }
    public async Task<User> TenantAsync(bool staff = false)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = new Tenant { FullName = "Test Tenant", NationalId = Guid.NewGuid().ToString("N"), Phone = "0990000000" };
        db.Tenants.Add(tenant); await db.SaveChangesAsync();
        var user = new User { FullName = tenant.FullName, UserName = "tenant-" + Guid.NewGuid().ToString("N"),
            Role = staff ? UserRole.TenantStaff : UserRole.Tenant, TenantId = tenant.Id };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordService>().Hash(user, "A-long-test-password-2026");
        db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    public HttpClient Client(User? user = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (user is not null)
        {
            using var scope = Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<JwtHelper>().GenerateToken(user);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }
    public async Task ChangeAsync(Action<AppDbContext> mutate)
    {
        using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        mutate(db); await db.SaveChangesAsync();
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _connection?.Dispose(); }
}
