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
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
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
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IVisitorWalletService, VisitorWalletService>();
builder.Services.AddScoped<DemandLetterPdfService>();

builder.Services.AddHostedService<SystemSchedulerService>();
builder.Services.AddSignalR();

// 5. الصلاحيات المتقدمة
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ═══════════════════════════════════════════════════════════
// 6. 🌐 CORS Policy (محددة للنطاقات الأربعة المطلوبة حصرياً)
// ═══════════════════════════════════════════════════════════
var allowedOrigins = new[]
{
    "https://admin.marinaalandalus.com",
    "https://tenant.marinaalandalus.com",
    "http://localhost:4300",
    "http://localhost:4200"
};

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

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
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
        }
    };
});

builder.Services.AddAuthorization();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddControllers();
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
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("🔄 التحقق من اتصال قاعدة البيانات وتشغيل البيانات الأولية...");
        await db.Database.MigrateAsync();
        await SettingsSeeder.SeedAsync(db);
        await UserSeeder.SeedAsync(db);
        logger.LogInformation("✅ تم تجهيز قاعدة البيانات بنجاح.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "⚠️ تنبيه: فشل الاتصال بقاعدة البيانات أثناء الإقلاع.");
    }
}

// ═══════════════════════════════════════════════════════════
// 9. خط سير المعالجة (Pipeline) المنضبط 100%
// ═══════════════════════════════════════════════════════════

// 👈 معالج سريع لطلبات OPTIONS قبل أي Middleware آخر لضمان عدم حجب المتصفح
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers["Origin"].ToString();
    if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
    {
        context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
        context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With, X-Test-Tenant-Id, X-Test-User-Id, access_token");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

        if (context.Request.Method == "OPTIONS")
        {
            context.Response.StatusCode = 200;
            await context.Response.CompleteAsync();
            return;
        }
    }
    await next();
});

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Andalos API v1");
    c.RoutePrefix = "swagger";
});

app.UseRouting();

// 👈 تفعيل الـ CORS Policy المحددة
app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();