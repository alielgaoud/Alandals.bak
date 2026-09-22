using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.System;
using Andalos.API.Interfaces;
using Andalos.API.Seed;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Andalos.API.Services
{
    public class SystemResetService : ISystemResetService
    {
        private readonly AppDbContext _db;

        public SystemResetService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<bool> ResetDatabaseToFactoryDefaultsAsync(ResetSystemDto dto, int currentUserId)
        {
            // 1. التحقق من أن المستخدم الحالي هو SuperAdmin
            var currentUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == currentUserId && u.IsActive);

            if (currentUser == null || !currentUser.UserName.Equals(SystemConstants.SuperAdminUserName, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("❌ غير مسموح: هذه العملية مصرحة فقط لمدير النظام الرئيسي الفعلي.");

            // 2. التحقق من صحة كلمة المرور المرفقة للتأكيد
            if (!VerifyPassword(dto.SuperAdminPassword, currentUser.PasswordHash))
                throw new UnauthorizedAccessException("❌ كلمة المرور المدخلة غير صحيحة. تم إلغاء عملية إعادة ضبط النظام.");

            // 3. بدء عملية التفريغ المرتبة بدقة حسب شجرة العلاقات
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // أ) مسح السجلات والتفاصيل الفرعية (Child Tables)
                await _db.AuditLogs.ExecuteDeleteAsync();
                await _db.EntryLogs.ExecuteDeleteAsync();
                await _db.PassTransactions.ExecuteDeleteAsync();
                await _db.TenantSettlements.ExecuteDeleteAsync();
                await _db.GatekeeperShifts.ExecuteDeleteAsync();
                await _db.VisitorPasses.ExecuteDeleteAsync();
                await _db.VisitorBlacklists.ExecuteDeleteAsync();

                await _db.ComplaintReplies.ExecuteDeleteAsync();
                await _db.Complaints.ExecuteDeleteAsync();
                await _db.BankTransferRequests.ExecuteDeleteAsync();

                await _db.Notifications.ExecuteDeleteAsync();
                await _db.PushSubscriptions.ExecuteDeleteAsync();
                await _db.NotificationPreferences.ExecuteDeleteAsync();

                // 👈 جديد: مسح طلبات الصيانة قبل مسح المحلات والمستأجرين
                await _db.MaintenanceRequests.ExecuteDeleteAsync();

                await _db.Refunds.ExecuteDeleteAsync();
                await _db.Expenses.ExecuteDeleteAsync();
                await _db.Payments.ExecuteDeleteAsync();

                await _db.ContractFees.ExecuteDeleteAsync();
                await _db.ContractItems.ExecuteDeleteAsync();
                await _db.ContractDocuments.ExecuteDeleteAsync();

                // ب) فك الارتباط الذاتي للعقود المجددة ثم مسح العقود
                await _db.Contracts.ExecuteUpdateAsync(c => c.SetProperty(b => b.ParentContractId, (int?)null));
                await _db.Contracts.ExecuteDeleteAsync();

                // ج) مسح المحلات والمستأجرين بعد فك ارتباط العقود والصيانة
                await _db.Units.ExecuteDeleteAsync();
                await _db.Tenants.ExecuteDeleteAsync();

                // د) مسح الصلاحيات والمستخدمين (باستثناء حساب superadmin الرئيسي)
                await _db.UserPermissionPackages.ExecuteDeleteAsync();
                await _db.PermissionPackageItems.ExecuteDeleteAsync();
                await _db.PermissionPackages.ExecuteDeleteAsync();
                await _db.UserPermissions.ExecuteDeleteAsync();

                await _db.Users
                    .Where(u => u.UserName != SystemConstants.SuperAdminUserName)
                    .ExecuteDeleteAsync();

                // هـ) تصفير عدادات الترقيم التسلسلي تبدأ من الصفر (0)
                await _db.NumberSequences.ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.LastNumber, 0)
                    .SetProperty(b => b.CurrentYear, DateTime.Now.Year)
                    .SetProperty(b => b.LastYear, DateTime.Now.Year));

                // و) إعادة ضبط الإعدادات للوضع الافتراضي إن طُلب ذلك
                if (dto.ResetSettingsToDefault)
                {
                    await _db.Settings.ExecuteDeleteAsync();
                    await SettingsSeeder.SeedAsync(_db);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private static bool VerifyPassword(string password, string hash)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes) == hash;
        }
    }
}