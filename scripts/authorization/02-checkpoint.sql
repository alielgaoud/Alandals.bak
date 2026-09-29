-- READ ONLY, after upgrade, offline SA rotation and reviewed grant/resource reconciliation.
SET NOCOUNT ON;
IF NOT EXISTS(SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId=N'20260929190000_DetailedAuthorization')
 THROW 51000,'Security migration missing.',1;
SELECT Id,Role,PermissionsVersion,PermissionsReconciled,RequiresPasswordChange,IsActive,IsLocked FROM Users;
SELECT UserId,PermissionKey,IsActive FROM DirectUserPermissions ORDER BY UserId,PermissionKey;
SELECT l.UserId,l.PackageId,l.IsActive LinkActive,p.IsActive PackageActive,i.PermissionKey,i.IsActive ItemActive
 FROM UserPermissionPackages l JOIN PermissionPackages p ON p.Id=l.PackageId JOIN PermissionPackageItems i ON i.PackageId=p.Id;
SELECT COUNT_BIG(*) LegacyUnreviewedPayments FROM Payments WHERE IsActive=1 AND AllocationRecorded=0;
SELECT COUNT_BIG(*) UnownedVisitorPasses FROM VisitorPasses WHERE OwnerTenantId IS NULL;
SELECT COUNT_BIG(*) UnreviewedPaidPasses FROM VisitorPasses p WHERE IsPaidPass=1 AND NOT EXISTS(SELECT 1 FROM GateCashReceipts r WHERE r.VisitorPassId=p.Id);
SELECT COUNT_BIG(*) QuarantinedPortalStaff FROM Users WHERE Role=6 AND CHARINDEX('|',COALESCE(Phone,''))>0;
SELECT n.Id,n.UserId,n.TenantId,n.TargetGroup FROM Notifications n LEFT JOIN Users u ON u.Id=n.UserId
 WHERE n.TargetGroup IS NOT NULL AND (n.UserId IS NOT NULL OR n.TenantId IS NOT NULL)
 OR (u.Role IN (5,6) AND n.TenantId IS NOT NULL AND n.TenantId<>u.TenantId);
-- Legacy arbitrary push endpoints are never trusted by delivery; counts only (no endpoint/key export).
SELECT COUNT_BIG(*) LegacyPushEndpointsToReview FROM PushSubscriptions WHERE IsActive=1 AND
 NOT (Endpoint LIKE 'https://fcm.googleapis.com/%' OR Endpoint LIKE 'https://updates.push.services.mozilla.com/%' OR Endpoint LIKE 'https://web.push.apple.com/%');
IF NOT EXISTS(SELECT 1 FROM Users WHERE Role=1 AND IsActive=1 AND IsLocked=0 AND RequiresPasswordChange=0 AND LEN(SecurityStamp)=32)
 THROW 51002,'No ready SA; use audited offline rotation/bootstrap and test login before reopening.',1;
