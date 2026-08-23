using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Services;
using Andalos.API.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);

// 1. Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. Helpers
builder.Services.AddSingleton<JwtHelper>();

// 3. Application Services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IVisitorPassService, VisitorPassService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<INumberGeneratorService, NumberGeneratorService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITenantPortalService, TenantPortalService>();

// =========================================================================
// 👈 إيقاف التحقق من التوكن وجعل كل الطلبات مفتوحة ومصرح لها كـ SuperAdmin
// =========================================================================
builder.Services.AddAuthentication("BypassAuth")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("BypassAuth", options => { });

builder.Services.AddAuthorization();
// =========================================================================

// 4. AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// تشغيل Seeder الإعدادات عند الإقلاع
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SettingsSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "حدث خطأ أثناء تشغيل Seeder الإعدادات.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// =========================================================================
// كلاس التجاوز التلقائي (يتيح كل العمليات لجميع الأدوار بدون توكن)
// =========================================================================
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "Admin"),
            new Claim(ClaimTypes.Email, "admin@andalos.ly"),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Accountant"),
            new Claim(ClaimTypes.Role, "GateKeeper")
        };
        var identity = new ClaimsIdentity(claims, "BypassAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "BypassAuth");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}