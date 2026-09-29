using Microsoft.AspNetCore.SignalR;
using Andalos.API.Security;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Andalos.API.Authorization;
using Andalos.API.Data;
using Andalos.API.Helpers;
using Andalos.API.Hubs;
using Andalos.API.Interfaces;
using Andalos.API.Seed;
using Andalos.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// 1. تفعيل ترخيص مكتبة الـ PDF المجاني
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════
// 2. Database
// ═══════════════════════════════════════════════════════════
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// 🛡️ تجميع السياقات (Pooling) لتحمّل الضغط: إعادة استخدام بدل إنشاء سياق لكل طلب
builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        // Do not transparently retry side-effecting writes. Serializable command transactions +
        // persisted idempotency receipts allow the client to retry explicitly and safely.
        sqlOptions.CommandTimeout(60);
    });
});

builder.Services.AddHttpContextAccessor();

// 3. Helpers & Caching
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddMemoryCache();

// 4. Application Services
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
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<UserSnapshotReader>();
builder.Services.AddScoped<EffectivePermissions>();
builder.Services.AddScoped<DelegationGuard>();
builder.Services.AddScoped<FinancialOperationGuard>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<PermissionUnionCache>();
builder.Services.AddSingleton<SecurityAuditWriter>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
builder.Services.AddScoped<AtomicOperationFilter>();
builder.Services.AddHostedService<EndpointSecurityStartupCheck>();
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
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<IVisitorWalletService, VisitorWalletService>();
builder.Services.AddScoped<DemandLetterPdfService>();
builder.Services.AddScoped<ISystemResetService, SystemResetService>();
builder.Services.AddScoped<ICircularService, CircularService>();
builder.Services.AddScoped<PermissionPackageService>();
builder.Services.AddScoped<IPermissionPackageService>(sp => sp.GetRequiredService<PermissionPackageService>());
builder.Services.AddHostedService<SystemSchedulerService>();
builder.Services.AddHostedService<NotificationDispatcher>();
builder.Services.AddSignalR(options => options.AddFilter<LiveIdentityHubFilter>());
builder.Services.AddScoped<LiveIdentityHubFilter>();

// 5. الصلاحيات المتقدمة
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IdentityAuthorizationHandler>();

// ═══════════════════════════════════════════════════════════
// 6. 🌐 CORS Policy (محددة للنطاقات الأربعة المطلوبة حصرياً)
// ═══════════════════════════════════════════════════════════
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (builder.Environment.IsDevelopment())
    allowedOrigins = allowedOrigins.Concat(new[] { "http://localhost:4300", "http://localhost:4200" }).Distinct().ToArray();
if (allowedOrigins.Any(o => o == "*" || !Uri.TryCreate(o, UriKind.Absolute, out _)))
    throw new InvalidOperationException("CORS requires exact approved origins.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // ضروري جداً لـ SignalR وتمرير التوكن
    });
});

// ═══════════════════════════════════════════════════════════
// 7. 🛡️ JWT Authentication
// ═══════════════════════════════════════════════════════════
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("❌ المفتاح JwtSettings:SecretKey غير موجود في appsettings.json!");

if (secretKey.Contains("SET_IN_", StringComparison.Ordinal) || Encoding.UTF8.GetByteCount(secretKey) < 32 ||
    string.IsNullOrWhiteSpace(jwtSettings["Issuer"]) || string.IsNullOrWhiteSpace(jwtSettings["Audience"]) ||
    jwtSettings.GetValue<int>("ExpiryMinutes", 60) is < 5 or > 1440)
    throw new InvalidOperationException("Configure a random JWT key (at least 32 bytes), issuer, audience and an expiry of 5-1440 minutes through deployment secrets.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = false;
    options.IncludeErrorDetails = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        ClockSkew = TimeSpan.Zero
    };

    // دعم SignalR لاستخراج التوكن من الـ Query String
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            context.HttpContext.Items["authorization.start"] = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var reader = context.HttpContext.RequestServices.GetRequiredService<UserSnapshotReader>();
                var current = context.HttpContext.RequestServices.GetRequiredService<CurrentUser>();
                if (!await reader.ValidateAsync(context.Principal!, current, context.HttpContext.RequestAborted)) context.Fail("Invalid session.");
            }
            catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>().LogError(ex, "Identity store unavailable.");
                context.HttpContext.Items["identity-store-unavailable"] = true;
                context.Fail("Identity store unavailable.");
            }
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = context.HttpContext.Items.ContainsKey("identity-store-unavailable") ? 503 : 401;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new { success = false, message = "تعذر التحقق من جلسة الدخول." });
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    foreach (var scope in new[] { IdentityPolicies.Authenticated, IdentityPolicies.Staff, IdentityPolicies.Tenant,
        IdentityPolicies.TenantOwner, IdentityPolicies.Self, IdentityPolicies.File, IdentityPolicies.Denied })
        options.AddPolicy(scope, p => p.RequireAuthenticatedUser().AddRequirements(new IdentityRequirement(scope)));
    foreach (var capability in PortalCapabilities.All)
        options.AddPolicy($"Portal.Capability.{capability}", p => p.RequireAuthenticatedUser().AddRequirements(new IdentityRequirement(IdentityPolicies.Tenant, capability)));
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(_ => false).Build();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-login", context => RateLimitPartition.GetSlidingWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new SlidingWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
});
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddControllers(options =>
{
    options.Conventions.Add(new EndpointSecurityConvention());
    options.Filters.AddService<AtomicOperationFilter>();
});
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Andalos API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "أدخل التوكن بهذا الشكل: Bearer {your token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ═══════════════════════════════════════════════════════════
// 8. DB Migration & Seeder
// ═══════════════════════════════════════════════════════════
if (args.Contains("--migrate") || args.Contains("--bootstrap-superadmin") || args.Contains("--rotate-superadmin"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (args.Contains("--migrate"))
    {
        await db.Database.MigrateAsync();
        await SettingsSeeder.SeedAsync(db);
    }
    if (args.Contains("--bootstrap-superadmin") || args.Contains("--rotate-superadmin"))
        await UserSeeder.BootstrapAsync(db, builder.Configuration, scope.ServiceProvider.GetRequiredService<PasswordService>(), args.Contains("--rotate-superadmin"));
    return;
}
if (builder.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Use the offline migration command for production.");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SettingsSeeder.SeedAsync(db);
}

// ═══════════════════════════════════════════════════════════
// 9. خط سير المعالجة (Pipeline) المنضبط 100%
// ═══════════════════════════════════════════════════════════

app.UseMiddleware<ApiSecurityMiddleware>();
// No unauthenticated static serving of uploads. ProtectedFilesController handles exact DB-owned files.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Andalos API v1"));
}

app.UseRouting();

// 👈 تفعيل الـ CORS Policy المحددة
app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseMiddleware<ClassifiedEndpointMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<IdempotencyRequestMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(IdentityPolicies.Authenticated);

app.Run();

public partial class Program { }