using Andalos.API.Data;
using Andalos.API.DTOs.System;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Seed;
using Andalos.API.Security;
using Microsoft.EntityFrameworkCore;
namespace Andalos.API.Services;
public sealed class SystemResetService(AppDbContext db, CurrentUser current, PasswordService passwords) : ISystemResetService
{
    public Task<bool> ResetDatabaseToFactoryDefaultsAsync(ResetSystemDto dto, int currentUserId) => db.AtomicAsync(async () =>
    {
        if (currentUserId != current.UserId || !current.Required.IsSuperAdmin) throw new ForbiddenOperationException();
        var actor = await db.Users.SingleAsync(u => u.Id == currentUserId && u.IsActive && !u.IsLocked && u.Role == UserRole.SuperAdmin);
        if (!passwords.Verify(actor, dto.SuperAdminPassword, out _)) throw new ForbiddenOperationException();
        // Audit and legacy permission provenance are NEVER erased by this HTTP operation.
        await db.EntryLogs.ExecuteDeleteAsync(); await db.GateCashReceipts.ExecuteDeleteAsync();
        await db.PassTransactions.ExecuteDeleteAsync(); await db.TenantSettlements.ExecuteDeleteAsync(); await db.GatekeeperShifts.ExecuteDeleteAsync();
        await db.VisitorPasses.ExecuteDeleteAsync(); await db.VisitorBlacklists.ExecuteDeleteAsync();
        await db.ComplaintReplies.ExecuteDeleteAsync(); await db.Complaints.ExecuteDeleteAsync(); await db.BankTransferRequests.ExecuteDeleteAsync();
        await db.Notifications.ExecuteDeleteAsync(); await db.PushSubscriptions.ExecuteDeleteAsync(); await db.NotificationPreferences.ExecuteDeleteAsync();
        await db.Refunds.ExecuteDeleteAsync(); await db.Expenses.ExecuteDeleteAsync(); await db.Payments.ExecuteDeleteAsync();
        await db.TenantCharges.ExecuteDeleteAsync(); await db.MaintenanceRequests.ExecuteDeleteAsync();
        await db.ContractFees.ExecuteDeleteAsync(); await db.ContractItems.ExecuteDeleteAsync(); await db.ContractDocuments.ExecuteDeleteAsync();
        await db.Contracts.ExecuteUpdateAsync(c => c.SetProperty(x => x.ParentContractId, (int?)null)); await db.Contracts.ExecuteDeleteAsync();
        await db.Units.ExecuteDeleteAsync(); await db.ProtectedDocuments.ExecuteDeleteAsync();
        var stamp = Guid.NewGuid().ToString("N");
        await db.Users.Where(u => u.Id != currentUserId).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false)
            .SetProperty(x => x.TenantId, (int?)null).SetProperty(x => x.SecurityStamp, stamp).SetProperty(x => x.PermissionsVersion, x => x.PermissionsVersion + 1));
        await db.Tenants.ExecuteDeleteAsync();
        await db.DirectUserPermissions.ExecuteDeleteAsync(); await db.UserPermissionPackages.ExecuteDeleteAsync();
        await db.PermissionPackageItems.ExecuteDeleteAsync(); await db.PermissionPackages.ExecuteDeleteAsync(); await db.TenantStaffPermissions.ExecuteDeleteAsync();
        await db.NumberSequences.ExecuteUpdateAsync(s => s.SetProperty(x => x.LastNumber, 0).SetProperty(x => x.CurrentYear, DateTime.UtcNow.Year));
        if (dto.ResetSettingsToDefault) { await db.Settings.ExecuteDeleteAsync(); await SettingsSeeder.SeedAsync(db); }
        actor.SecurityStamp = Guid.NewGuid().ToString("N");
        db.AuditLogs.Add(new() { UserId = actor.Id, AuditType = "FactoryReset", TableName = "System", PrimaryKey = "operational", Outcome = "Success",
            NewValues = "{\"preserved\":[\"AuditLogs\",\"legacy permissions\",\"idempotency receipts\",\"actor\"]}" });
        await db.SaveChangesAsync();
        return true; // all old sessions revoked; login again.
    });
}
