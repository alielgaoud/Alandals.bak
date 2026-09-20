using Andalos.API.Authorization;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Hubs;
using Andalos.API.Interfaces;
using Andalos.API.Seed;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

// 1. تفعيل ترخيص مكتبة الـ PDF المجاني
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// 2. Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Helpers
builder.Services.AddSingleton<JwtHelper>();

// 4. Application Services
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
builder.Services.AddScoped<ContractPdfService>();
builder.Services.AddScoped<ReceiptPdfService>();
builder.Services.AddScoped<ITenantAccountService, TenantAccountService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<ReportPdfService>();
builder.Services.AddScoped<IVisitorBlacklistService, VisitorBlacklistService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
builder.Services.AddScoped<ComplaintReportPdfService>();
builder.Services.AddScoped<IBankTransferService, BankTransferService>();

// خدمات الـ Push والـ Scheduler والـ Wallet والـ Demand Letters
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddHostedService<SystemSchedulerService>(); // 👈 تسجيل المحرك الخلفي
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IVisitorWalletService, VisitorWalletService>();
builder.Services.AddScoped<DemandLetterPdfService>();

// 👈 جديد: SignalR للإشعارات اللحظية
builder.Services.AddSignalR();

// صلاحيات متقدمة (جاهزة لكن غير مفعلة بالكامل أثناء التطوير)
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// 5. CORS (محدث لدعم SignalR)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true) // يسمح لأي Origin أثناء التطوير
              .AllowCredentials();           // 👈 ضروري لـ SignalR
    });
});

// 6. إيقاف التحقق من التوكن مؤقتاً لتسهيل الاختبار
builder.Services.AddAuthentication("BypassAuth")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("BypassAuth", options => { });

builder.Services.AddAuthorization();

// 7. AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseStaticFiles();

// 👈 تشغيل Seeder الإعدادات والحساب الافتراضي المحمي عند الإقلاع
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SettingsSeeder.SeedAsync(db);
        await UserSeeder.SeedAsync(db); // 👈 تشغيل زارع مدير النظام الافتراضي المحمي
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "حدث خطأ أثناء تشغيل Seeder الإعدادات أو المستخدمين.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// 👈 CORS قبل Authentication
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 👈 تسجيل Hub الإشعارات
app.MapHub<NotificationHub>("/hubs/notifications");

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
        // 💡 قراءة سياق الاختبار من الـ Headers إن وجدت لمحاكاة مستأجر حقيقي
        var hasTenantHeader = Request.Headers.TryGetValue("X-Test-Tenant-Id", out var tenantIdStr);
        var hasUserHeader = Request.Headers.TryGetValue("X-Test-User-Id", out var userIdStr);

        var claims = new List<Claim>();

        if (hasTenantHeader && int.TryParse(tenantIdStr, out var tenantId))
        {
            // === محاكاة مستأجر حقيقي ===
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userIdStr.ToString() ?? "2"));
            claims.Add(new Claim(ClaimTypes.Name, "TenantUser"));
            claims.Add(new Claim(ClaimTypes.Email, "tenant@andalos.ly"));
            claims.Add(new Claim(ClaimTypes.Role, "Tenant"));
            claims.Add(new Claim("TenantId", tenantId.ToString())); // 👈 شحن معرف المستأجر المهم جداً
        }
        else
        {
            // === الافتراضي: محاكاة مدير نظام ===
            claims.Add(new Claim(ClaimTypes.NameIdentifier, "1"));
            claims.Add(new Claim(ClaimTypes.Name, "Admin"));
            claims.Add(new Claim(ClaimTypes.Email, "admin@andalos.ly"));
            claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Accountant"));
            claims.Add(new Claim(ClaimTypes.Role, "GateKeeper"));
        }

        var identity = new ClaimsIdentity(claims, "BypassAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "BypassAuth");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}