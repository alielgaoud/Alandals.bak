-- READ ONLY, before upgrade. Run on a restored production copy first. sqlcmd -b -i ...
SET NOCOUNT ON;
IF (SELECT MAX(MigrationId) FROM __EFMigrationsHistory) <> N'20260926230000_AddChargeApprovalWorkflow'
 OR NOT EXISTS(SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId=N'20260926230000_AddChargeApprovalWorkflow')
 THROW 51000, 'Unexpected baseline; investigate schema/version before migration.', 1;
DECLARE @Known nvarchar(max)=N'BankTransfers.Review,BankTransfers.View,Circulars.Create,Circulars.Delete,Circulars.Edit,Circulars.View,Complaints.ExportPdf,Complaints.Reply,Complaints.UpdateStatus,Complaints.View,Contracts.Create,Contracts.Delete,Contracts.Edit,Contracts.ExportPdf,Contracts.Renew,Contracts.UpdateStatus,Contracts.View,Demands.GeneratePdf,Demands.SendToTenant,Expenses.Create,Expenses.Delete,Expenses.View,Financials.ChargeTenant,Financials.CreatePayment,Financials.CreateRefund,Financials.DeletePayment,Financials.DeleteRefund,Financials.DepositAdvance,Financials.ProcessMonthlyDues,Financials.SettleCharge,Financials.ViewCharges,Financials.ViewPayments,Financials.ViewRefunds,Gate.Scan,Gate.ViewLogs,Maintenance.Create,Maintenance.Delete,Maintenance.EditStatus,Maintenance.View,Notifications.Send,Reports.ViewDashboard,Reports.ViewFinancialReports,Reports.ViewOccupancyReports,Reports.ViewVisitorTrafficReports,Settings.Edit,Settings.Reset,Settings.ResetDatabase,Settings.View,Tenants.Create,Tenants.Delete,Tenants.Edit,Tenants.View,Tenants.ViewBalances,Tenants.ViewStatement,Units.Create,Units.Delete,Units.Edit,Units.View,Units.ViewHistory,Users.Create,Users.Delete,Users.Edit,Users.ManagePermissions,Users.ResetPassword,Users.ToggleLock,Users.View,Users.ViewAuditLogs,VisitorWallet.AddBalanceToPass,VisitorWallet.HandoverShift,VisitorWallet.IssuePaidPass,VisitorWallet.SettleShopBalance,VisitorWallet.ViewAllShopsBalances,VisitorWallet.ViewGateCashReport,VisitorWallet.ViewMyShiftSummary,VisitorWallet.ViewTenantHistory,Visitors.CheckBlacklist,Visitors.Create,Visitors.ManageBlacklist,Visitors.RevokePass,Visitors.View';
SELECT MigrationId,ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;
SELECT Role,IsActive,IsLocked,COUNT_BIG(*) UsersCount FROM Users GROUP BY Role,IsActive,IsLocked;
-- Reachability requires offline password verification as well, not merely an unlocked row.
SELECT Id,Role,IsActive,IsLocked,CASE WHEN LEN(PasswordHash)=44 THEN 1 ELSE 0 END IsLegacySha
 FROM Users WHERE Role=1; -- No hash/password/stamp output.
SELECT UserId,COUNT_BIG(*) LegacyRows FROM UserPermissions GROUP BY UserId;
-- The intersection cannot distinguish manually/direct granted keys from flattened package keys.
SELECT up.UserId,up.PermissionKey,link.PackageId,up.IsActive LegacyActive,link.IsActive LinkActive,p.IsActive PackageActive,pi.IsActive ItemActive
 FROM UserPermissions up LEFT JOIN UserPermissionPackages link ON link.UserId=up.UserId
 LEFT JOIN PermissionPackages p ON p.Id=link.PackageId
 LEFT JOIN PermissionPackageItems pi ON pi.PackageId=link.PackageId AND pi.PermissionKey=up.PermissionKey;
SELECT 'LegacyUserPermission' Source,UserId Owner,PermissionKey FROM UserPermissions
 WHERE PermissionKey COLLATE Latin1_General_100_BIN2 NOT IN (SELECT value COLLATE Latin1_General_100_BIN2 FROM STRING_SPLIT(@Known,','))
 UNION ALL SELECT 'PackageItem',PackageId,PermissionKey FROM PermissionPackageItems
 WHERE PermissionKey COLLATE Latin1_General_100_BIN2 NOT IN (SELECT value COLLATE Latin1_General_100_BIN2 FROM STRING_SPLIT(@Known,','));
SELECT UserId,COUNT_BIG(*) OpenShifts FROM GatekeeperShifts WHERE IsActive=1 AND IsHandedOver=0 GROUP BY UserId HAVING COUNT_BIG(*)>1;
SELECT COUNT_BIG(*) LegacyPaymentsRequiringAllocationReview FROM Payments WHERE IsActive=1;
SELECT COUNT_BIG(*) PaidPassesWithoutReviewedCashLedger FROM VisitorPasses WHERE IsPaidPass=1;
SELECT COUNT_BIG(*) LegacyUploadDocuments FROM ContractDocuments;
SELECT COUNT_BIG(*) LegacyTransferAttachments FROM BankTransferRequests WHERE ReceiptFilePath IS NOT NULL;
-- Recognized portal capability suffixes require operator review. No phone/PII contents exported here.
SELECT Id,TenantId,Role FROM Users WHERE Role=6 AND CHARINDEX('|',COALESCE(Phone,''))>0;
IF EXISTS(SELECT 1 FROM GatekeeperShifts WHERE IsActive=1 AND IsHandedOver=0 GROUP BY UserId HAVING COUNT_BIG(*)>1)
 THROW 51001,'Duplicate open shifts: reviewed financial resolution required; do not auto-delete.',1;
