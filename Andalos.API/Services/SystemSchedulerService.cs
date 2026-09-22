using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class SystemSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemSchedulerService> _logger;

        // متغيرات تمنع تكرار تنفيذ المهمة أكثر من مرة في نفس اليوم/الشهر
        private int? _lastExpiredCheckDay;
        private int? _lastMonthlyDueCheckMonth;

        public SystemSchedulerService(IServiceProvider serviceProvider, ILogger<SystemSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚀 بدء تشغيل محرك المهام الخلفية للنظام...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTimeHelper.LibyaNow; // 👈 قراءة الساعة المحلية لليبيا

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();

                        // 👈 1. قراءة "ساعة تصفير محفظة الزوار" ديناميكياً من الإعدادات (الافتراضي: 3 فجراً)
                        int walletExpirationHour = await settings.GetValueAsync<int>(SettingKeys.VisitorWalletExpirationHour, 3);

                        if (now.Hour == walletExpirationHour && _lastExpiredCheckDay != now.Day)
                        {
                            _logger.LogInformation($"⏰ جاري فحص وتصفير أرصدة محفظة الزوار منتهية الصلاحية (الساعة المحددة بالإعدادات: {walletExpirationHour}:00)...");

                            var walletService = scope.ServiceProvider.GetRequiredService<IVisitorWalletService>();
                            decimal totalExpiredProfit = await walletService.ExpireUnusedBalancesAsync();

                            if (totalExpiredProfit > 0)
                            {
                                _logger.LogInformation($"💰 تم تصفير أرصدة الزوار المنتهية وتحويل ({totalExpiredProfit} د.ل) كربح صافي للإدارة.");
                            }

                            _lastExpiredCheckDay = now.Day; // ضمان عدم التكرار في نفس اليوم
                        }

                        // 👈 نشر التعاميم المجدولة المستحقة
                        var circularService = scope.ServiceProvider.GetRequiredService<ICircularService>();
                        int publishedCirculars = await circularService.PublishDueScheduledCircularsAsync();
                        if (publishedCirculars > 0)
                            _logger.LogInformation($"📢 تم نشر {publishedCirculars} تعميم مجدول.");

                        // 👈 2. الخصم الشهري الآلي للإيجارات (عند الساعة 1 ليلاً يوم 1 في الشهر)
                        if (now.Hour == 1)
                        {
                            if (now.Day == 1 && _lastMonthlyDueCheckMonth != now.Month)
                            {
                                _logger.LogInformation("💰 بدء عملية الخصم الشهري الآلي للإيجارات...");
                                var accountService = scope.ServiceProvider.GetRequiredService<ITenantAccountService>();
                                await accountService.ProcessMonthlyRentDuesAsync();

                                _lastMonthlyDueCheckMonth = now.Month;
                            }

                            // 👈 3. فحص العقود التي ستنتهي قريباً (بعد 30 يوم)
                            await CheckExpiringContractsAsync(scope);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ حدث خطأ في محرك المهام الخلفية");
                }

                // انتظار 30 دقيقة قبل الفحص التالي
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        private async Task CheckExpiringContractsAsync(IServiceScope scope)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var targetDate = DateTime.Today.AddDays(30);

            var expiringContracts = await db.Contracts
                .Include(c => c.Tenant)
                .Where(c => c.IsActive && c.Status == ContractStatus.Active && c.EndDate.Date == targetDate.Date)
                .ToListAsync();

            foreach (var contract in expiringContracts)
            {
                // إشعار للإدارة
                await notificationService.SendToAllAdminsAsync(
                    "عقد قارب على الانتهاء",
                    $"العقد رقم {contract.ContractNumber} للمستأجر {contract.Tenant?.FullName} سينتهي بعد 30 يوم.",
                    NotificationType.ContractExpiringSoon,
                    NotificationPriority.High,
                    $"/admin/contracts/{contract.Id}");

                // إشعار للمستأجر
                if (contract.TenantId > 0)
                {
                    await notificationService.SendToTenantAsync(
                        contract.TenantId,
                        "تذكير بانتهاء العقد",
                        $"عزيزي المستأجر، عقدك رقم {contract.ContractNumber} سينتهي بتاريخ {contract.EndDate:yyyy/MM/dd}. يرجى مراجعة الإدارة.",
                        NotificationType.ContractExpiringSoon,
                        $"/tenant/contracts");
                }
            }
        }
    }
}