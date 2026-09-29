using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace Andalos.API.Migrations;
public partial class DetailedAuthorization : Migration
{
    private static readonly string[] RevisionTables = { "Users", "PermissionPackages", "Tenants", "VisitorPasses", "GatekeeperShifts",
        "TenantCharges", "BankTransferRequests", "Payments", "Refunds", "MaintenanceRequests", "PassTransactions", "NumberSequences", "Contracts" };
    protected override void Up(MigrationBuilder m)
    {
        // PRECONDITION: this is a maintenance-window migration. Never mix old sync writers with the new API.
        m.Sql("IF EXISTS (SELECT UserId FROM dbo.GatekeeperShifts WHERE IsActive=1 AND IsHandedOver=0 GROUP BY UserId HAVING COUNT(*)>1) THROW 51000, 'Duplicate open shifts: reconcile audited cash totals before migration.', 1;");
        foreach (var table in RevisionTables)
            m.AddColumn<Guid>("Revision", table, type: "uniqueidentifier", nullable: false, defaultValue: Guid.Empty);
        m.AddColumn<string>("SecurityStamp", "Users", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "");
        m.AddColumn<long>("PermissionsVersion", "Users", type: "bigint", nullable: false, defaultValue: 1L);
        m.AddColumn<bool>("PermissionsReconciled", "Users", type: "bit", nullable: false, defaultValue: false);
        m.AddColumn<bool>("RequiresPasswordChange", "Users", type: "bit", nullable: false, defaultValue: false);
        m.AddColumn<int>("OwnerTenantId", "VisitorPasses", type: "int", nullable: true);
        m.AddColumn<int>("HandedOverByUserId", "GatekeeperShifts", type: "int", nullable: true);
        m.AddColumn<int>("ReviewedByUserId", "BankTransferRequests", type: "int", nullable: true);
        m.AddColumn<DateTime>("ReviewedAt", "BankTransferRequests", type: "datetime2", nullable: true);
        m.AddColumn<DateTime>("RealtimeDeliveredAt", "Notifications", type: "datetime2", nullable: true);
        m.AddColumn<string>("Outcome", "AuditLogs", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Success");
        m.AddColumn<string>("CorrelationId", "AuditLogs", type: "nvarchar(100)", maxLength: 100, nullable: true);
        m.AddColumn<bool>("AllocationRecorded", "Payments", type: "bit", nullable: false, defaultValue: false);
        m.AddColumn<decimal>("WalletCreditAmount", "Payments", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        m.AddColumn<decimal>("WalletDebitAmount", "Payments", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        m.AddColumn<decimal>("AllocatedChargeAmount", "Payments", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        m.AddColumn<int>("AllocatedChargeId", "Payments", type: "int", nullable: true);
        m.AddColumn<string>("SystemOperationKey", "Payments", type: "nvarchar(80)", maxLength: 80, nullable: true);
        m.CreateTable("DirectUserPermissions", columns: t => new
        {
            Id = t.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            UserId = t.Column<int>("int", nullable: false), PermissionKey = t.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            CreatedAt = t.Column<DateTime>("datetime2", nullable: false), UpdatedAt = t.Column<DateTime>("datetime2", nullable: true),
            CreatedBy = t.Column<string>("nvarchar(max)", nullable: true), UpdatedBy = t.Column<string>("nvarchar(max)", nullable: true), IsActive = t.Column<bool>("bit", nullable: false)
        }, constraints: t => { t.PrimaryKey("PK_DirectUserPermissions", x => x.Id); t.ForeignKey("FK_DirectUserPermissions_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade); });
        m.CreateTable("TenantStaffPermissions", columns: t => new
        {
            Id = t.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), UserId = t.Column<int>("int", nullable: false),
            Capability = t.Column<string>("nvarchar(30)", maxLength: 30, nullable: false)
        }, constraints: t => { t.PrimaryKey("PK_TenantStaffPermissions", x => x.Id); t.ForeignKey("FK_TenantStaffPermissions_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Cascade); });
        m.CreateTable("IdempotencyRecords", columns: t => new
        {
            Id = t.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), UserId = t.Column<int>("int", nullable: false),
            Operation = t.Column<string>("nvarchar(180)", maxLength: 180, nullable: false), KeyHash = t.Column<string>("nvarchar(64)", maxLength: 64, nullable: false),
            RequestHash = t.Column<string>("nvarchar(64)", maxLength: 64, nullable: false), StatusCode = t.Column<int>("int", nullable: false),
            ResponseJson = t.Column<string>("nvarchar(max)", nullable: false), Location = t.Column<string>("nvarchar(1000)", maxLength: 1000, nullable: true), CreatedAt = t.Column<DateTime>("datetime2", nullable: false)
        }, constraints: t => { t.PrimaryKey("PK_IdempotencyRecords", x => x.Id); t.ForeignKey("FK_IdempotencyRecords_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict); });
        m.CreateTable("GateCashReceipts", columns: t => new
        {
            Id = t.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), VisitorPassId = t.Column<int>("int", nullable: false),
            ShiftId = t.Column<int>("int", nullable: true), UserId = t.Column<int>("int", nullable: false), Amount = t.Column<decimal>("decimal(18,2)", nullable: false),
            Kind = t.Column<string>("nvarchar(20)", maxLength: 20, nullable: false), CreatedAt = t.Column<DateTime>("datetime2", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_GateCashReceipts", x => x.Id);
            t.ForeignKey("FK_GateCashReceipts_VisitorPasses_VisitorPassId", x => x.VisitorPassId, "VisitorPasses", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_GateCashReceipts_Users_UserId", x => x.UserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_GateCashReceipts_GatekeeperShifts_ShiftId", x => x.ShiftId, "GatekeeperShifts", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateTable("ProtectedDocuments", columns: t => new
        {
            Id = t.Column<int>("int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), Path = t.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            TenantId = t.Column<int>("int", nullable: false), PermissionKey = t.Column<string>("nvarchar(100)", maxLength: 100, nullable: false), CreatedAt = t.Column<DateTime>("datetime2", nullable: false)
        }, constraints: t => { t.PrimaryKey("PK_ProtectedDocuments", x => x.Id); t.ForeignKey("FK_ProtectedDocuments_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict); });
        m.CreateIndex("IX_DirectUserPermissions_UserId_PermissionKey", "DirectUserPermissions", new[] { "UserId", "PermissionKey" }, unique: true);
        m.CreateIndex("IX_DirectUserPermissions_UserId_IsActive", "DirectUserPermissions", new[] { "UserId", "IsActive" });
        m.CreateIndex("IX_TenantStaffPermissions_UserId_Capability", "TenantStaffPermissions", new[] { "UserId", "Capability" }, unique: true);
        m.CreateIndex("IX_IdempotencyRecords_UserId_Operation_KeyHash", "IdempotencyRecords", new[] { "UserId", "Operation", "KeyHash" }, unique: true);
        m.CreateIndex("IX_IdempotencyRecords_CreatedAt", "IdempotencyRecords", "CreatedAt");
        m.CreateIndex("IX_GateCashReceipts_UserId_CreatedAt", "GateCashReceipts", new[] { "UserId", "CreatedAt" });
        m.CreateIndex("IX_GateCashReceipts_VisitorPassId", "GateCashReceipts", "VisitorPassId");
        m.CreateIndex("IX_GateCashReceipts_ShiftId", "GateCashReceipts", "ShiftId");
        m.CreateIndex("IX_ProtectedDocuments_Path", "ProtectedDocuments", "Path", unique: true);
        m.CreateIndex("IX_ProtectedDocuments_TenantId", "ProtectedDocuments", "TenantId");
        m.CreateIndex("IX_Users_IsActive_IsLocked_Role", "Users", new[] { "IsActive", "IsLocked", "Role" });
        m.CreateIndex("IX_UserPermissionPackages_PackageId_IsActive_UserId", "UserPermissionPackages", new[] { "PackageId", "IsActive", "UserId" });
        m.CreateIndex("IX_PermissionPackageItems_PackageId_IsActive", "PermissionPackageItems", new[] { "PackageId", "IsActive" });
        m.CreateIndex("IX_VisitorPasses_OwnerTenantId_IsActive", "VisitorPasses", new[] { "OwnerTenantId", "IsActive" });
        m.CreateIndex("IX_Payments_AllocatedChargeId", "Payments", "AllocatedChargeId");
        m.CreateIndex("IX_Payments_SystemOperationKey", "Payments", "SystemOperationKey", unique: true, filter: "[SystemOperationKey] IS NOT NULL");
        m.CreateIndex("IX_Notifications_RealtimeDeliveredAt_IsActive", "Notifications", new[] { "RealtimeDeliveredAt", "IsActive" });
        m.DropIndex("IX_GatekeeperShifts_UserId", "GatekeeperShifts");
        m.CreateIndex("IX_GatekeeperShifts_OpenUser", "GatekeeperShifts", "UserId", unique: true, filter: "[IsActive] = 1 AND [IsHandedOver] = 0");
        m.AddForeignKey("FK_Payments_TenantCharges_AllocatedChargeId", "Payments", "AllocatedChargeId", "TenantCharges", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        m.AddForeignKey("FK_VisitorPasses_Tenants_OwnerTenantId", "VisitorPasses", "OwnerTenantId", "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        m.Sql(@"
UPDATE dbo.Users SET SecurityStamp=REPLACE(CONVERT(nvarchar(36),NEWID()),'-',''), PermissionsVersion=1;
-- No heuristic migration into direct grants: the legacy mixed table is retained intact for review/rollback.
UPDATE u SET PermissionsReconciled=CASE WHEN u.Role IN (5,6) OR
 (NOT EXISTS(SELECT 1 FROM dbo.UserPermissions p WHERE p.UserId=u.Id AND p.IsActive=1)
  AND NOT EXISTS(SELECT 1 FROM dbo.UserPermissionPackages l WHERE l.UserId=u.Id)) THEN 1 ELSE 0 END
FROM dbo.Users u;
UPDATE dbo.Users SET RequiresPasswordChange=1 WHERE Role=1 AND LEN(PasswordHash)=44;
-- Never re-push old notifications during rollout.
UPDATE dbo.Notifications SET RealtimeDeliveredAt=SYSUTCDATETIME() WHERE IsSent=1;
-- No portal-capability, phone, historical cash, pass-owner or file-ownership guesses.
-- Keep all legacy values for reviewed reconciliation; new capability/receipt/document tables stay empty.
-- OwnerTenantId remains NULL for historical passes until evidence is reviewed by the operator.
-- Redact previously collected secrets; backup retention must be handled by the operator too.
UPDATE dbo.AuditLogs SET OldValues=NULL,NewValues=NULL,AffectedColumns='[""redacted legacy secrets""]'
WHERE COALESCE(OldValues,'') LIKE '%PasswordHash%' OR COALESCE(NewValues,'') LIKE '%PasswordHash%'
 OR TableName='PushSubscription'
 OR (TableName='Setting' AND (COALESCE(OldValues,'') LIKE '%PrivateKey%' OR COALESCE(NewValues,'') LIKE '%PrivateKey%'
     OR COALESCE(OldValues,'') LIKE '%Secret%' OR COALESCE(NewValues,'') LIKE '%Secret%'));
");
    }
    protected override void Down(MigrationBuilder m)
    {
        // Deliberate rollback fence: dropping reconciled grants/replay receipts would silently restore unsafe old authorization.
        throw new NotSupportedException("Destructive downgrade is intentionally blocked. Stop traffic, restore the pre-cutover database/files backup and patched recovery build per docs/authorization/DEPLOYMENT.ar.md.");
    }
}
