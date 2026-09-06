using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Microsoft.EntityFrameworkCore;
using Andalos.API.Data;

namespace Andalos.API.Services
{
    public class SystemSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemSchedulerService> _logger;

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
                    // نُشغل المهام اليومية مرة واحدة كل 24 ساعة (أو كل ساعة حسب ما تفضل)
                    // للتبسيط: سنفحص كل ساعة، لكن سننفذ المهام إذا كانت الساعة بين 1 و 2 ليلاً
                    var now = DateTime.Now;

                    if (now.Hour == 1) // يعمل الواحدة ليلاً
                    {
                        using var scope = _serviceProvider.CreateScope();

                        // 1. الخصم الآلي للإيجارات بداية كل شهر
                        if (now.Day == 1)
                        {
                            _logger.LogInformation("💰 بدء عملية الخصم الشهري الآلي للإيجارات...");
                            var accountService = scope.ServiceProvider.GetRequiredService<ITenantAccountService>();
                            await accountService.ProcessMonthlyRentDuesAsync();
                        }

                        // 2. فحص العقود التي ستنتهي قريباً (بعد 30 يوم)
                        await CheckExpiringContractsAsync(scope);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ حدث خطأ في محرك المهام الخلفية");
                }

                // انتظار ساعة قبل الفحص التالي
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
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