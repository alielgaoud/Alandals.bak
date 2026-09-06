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
// خدمات الـ Push والـ Scheduler
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddHostedService<SystemSchedulerService>(); // 👈 تسجيل المحرك الخلفي
// 👈 جديد: خدمة الإشعارات
builder.Services.AddScoped<INotificationService, NotificationService>();

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