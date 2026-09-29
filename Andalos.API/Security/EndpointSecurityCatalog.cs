using Andalos.API.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Andalos.API.Security;

public sealed record EndpointSecurityRule(string Scope, string[] PermissionKeys, string? Roles = null,
    string? Capability = null, bool IsPublic = false, bool IsIdempotent = false, string Notes = "")
{
    public EndpointSecurityRule WithRoles(string roles) => this with { Roles = roles };
    public string IdentityPolicy => Capability is null ? Scope : $"Portal.Capability.{Capability}";
}

// One explicit classification per real action. No HTTP-verb or role-derived permission inference.
public static class EndpointSecurityCatalog
{
    private static EndpointSecurityRule Staff(params string[] keys) => new(IdentityPolicies.Staff, keys);
    private static EndpointSecurityRule Identity(string scope, string? capability = null) => new(scope, Array.Empty<string>(), Capability: capability);
    private static EndpointSecurityRule Public() => new(IdentityPolicies.Authenticated, Array.Empty<string>(), IsPublic: true);
    private static EndpointSecurityRule Denied() => new(IdentityPolicies.Denied, Array.Empty<string>());
    public static readonly IReadOnlyDictionary<string, EndpointSecurityRule> Rules = new Dictionary<string, EndpointSecurityRule>(StringComparer.Ordinal)
    {
        ["Auth.ChangePassword"] = Identity(IdentityPolicies.Authenticated),
        ["Auth.Login"] = Public(),
        ["Auth.Logout"] = Identity(IdentityPolicies.Authenticated),
        ["Auth.MePermissions"] = Identity(IdentityPolicies.Staff),
        ["Auth.Register"] = Public() with { Notes = "Registration disabled; login returns no role-default grants." },
        ["Auth.TenantLogin"] = Public(),
        ["BankTransfers.GetAllPending"] = Staff(Permissions.BankTransfers.View),
        ["BankTransfers.Review"] = Staff(Permissions.BankTransfers.Review) with { IsIdempotent = true },
        ["Circulars.Create"] = Staff(Permissions.Circulars.Create),
        ["Circulars.Delete"] = Staff(Permissions.Circulars.Delete),
        ["Circulars.GetAll"] = Staff(Permissions.Circulars.View),
        ["Circulars.GetById"] = Staff(Permissions.Circulars.View),
        ["Circulars.Update"] = Staff(Permissions.Circulars.Edit),
        ["Complaints.DownloadReport"] = Staff(Permissions.Complaints.ExportPdf),
        ["Complaints.GetAll"] = Staff(Permissions.Complaints.View),
        ["Complaints.GetById"] = Staff(Permissions.Complaints.View),
        ["Complaints.Reply"] = Staff(Permissions.Complaints.Reply),
        ["Complaints.UpdateStatus"] = Staff(Permissions.Complaints.UpdateStatus),
        ["Contracts.Create"] = Staff(Permissions.Contracts.Create),
        ["Contracts.Delete"] = Staff(Permissions.Contracts.Delete),
        ["Contracts.GetAll"] = Staff(Permissions.Contracts.View),
        ["Contracts.GetById"] = Staff(Permissions.Contracts.View),
        ["Contracts.Renew"] = Staff(Permissions.Contracts.Renew) with { IsIdempotent = true },
        ["Contracts.Update"] = Staff(Permissions.Contracts.Edit),
        ["Contracts.UpdateStatus"] = Staff(Permissions.Contracts.UpdateStatus),
        ["DemandLetters.GeneratePdf"] = Staff(Permissions.Demands.GeneratePdf),
        ["DemandLetters.GeneratePdfGet"] = Staff(Permissions.Demands.GeneratePdf),
        ["DemandLetters.SendDemand"] = Staff(Permissions.Demands.GeneratePdf, Permissions.Demands.SendToTenant),
        ["Expenses.Create"] = Staff(Permissions.Expenses.Create),
        ["Expenses.Delete"] = Staff(Permissions.Expenses.Delete),
        ["Expenses.GetAll"] = Staff(Permissions.Expenses.View),
        ["Expenses.GetByTenant"] = Staff(Permissions.Expenses.View),
        ["Expenses.GetByUnit"] = Staff(Permissions.Expenses.View),
        ["Expenses.GetTotal"] = Staff(Permissions.Expenses.View),
        ["Gate.GetLogs"] = Staff(Permissions.Gate.ViewLogs),
        ["Gate.Scan"] = Staff(Permissions.Gate.Scan) with { IsIdempotent = true },
        ["Maintenance.ChargeTenant"] = Staff(Permissions.Financials.ChargeTenant) with { IsIdempotent = true },
        ["Maintenance.Create"] = Staff(Permissions.Maintenance.Create),
        ["Maintenance.Delete"] = Staff(Permissions.Maintenance.Delete),
        ["Maintenance.GetAll"] = Staff(Permissions.Maintenance.View),
        ["Maintenance.GetById"] = Staff(Permissions.Maintenance.View),
        ["Maintenance.GetByUnit"] = Staff(Permissions.Maintenance.View),
        ["Maintenance.GetCharge"] = Staff(Permissions.Financials.ViewCharges),
        ["Maintenance.GetCharges"] = Staff(Permissions.Financials.ViewCharges),
        ["Maintenance.SettleCharge"] = Staff(Permissions.Financials.SettleCharge) with { IsIdempotent = true },
        ["Maintenance.UpdateStatus"] = Staff(Permissions.Maintenance.EditStatus) with { IsIdempotent = true, Notes = "Extra Financials.ChargeTenant if billing; Expenses.Create if recording cost. Enforced in service." },
        ["Notifications.Delete"] = Identity(IdentityPolicies.Self),
        ["Notifications.GetAll"] = Identity(IdentityPolicies.Self),
        ["Notifications.GetPreferences"] = Identity(IdentityPolicies.Self),
        ["Notifications.GetSummary"] = Identity(IdentityPolicies.Self),
        ["Notifications.GetUnreadCount"] = Identity(IdentityPolicies.Self),
        ["Notifications.GetVapidPublicKey"] = Public() with { Notes = "Public key only, no writes or secret values." },
        ["Notifications.MarkAllAsRead"] = Identity(IdentityPolicies.Self),
        ["Notifications.MarkAsRead"] = Identity(IdentityPolicies.Self),
        ["Notifications.SubscribeToPush"] = Identity(IdentityPolicies.Self),
        ["Notifications.TestSend"] = Staff(Permissions.Notifications.Send),
        ["Notifications.UpdatePreferences"] = Identity(IdentityPolicies.Self),
        ["Payments.Create"] = Staff(Permissions.Financials.CreatePayment) with { IsIdempotent = true, Notes = "Extra Financials.DepositAdvance for OnAccount/overflow; Financials.SettleCharge for charge allocation." },
        ["Payments.Delete"] = Staff(Permissions.Financials.DeletePayment) with { IsIdempotent = true },
        ["Payments.GetAll"] = Staff(Permissions.Financials.ViewPayments),
        ["Payments.GetAllSummaries"] = Staff(Permissions.Tenants.ViewBalances),
        ["Payments.GetByContract"] = Staff(Permissions.Financials.ViewPayments),
        ["Payments.GetByTenant"] = Staff(Permissions.Financials.ViewPayments),
        ["Payments.GetContractSummary"] = Staff(Permissions.Tenants.ViewBalances),
        ["Pdf.DownloadContract"] = Staff(Permissions.Contracts.ExportPdf),
        ["Pdf.DownloadReceipt"] = Staff(Permissions.Financials.ViewPayments),
        ["Pdf.FinancialReport"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Pdf.OccupancyReport"] = Staff(Permissions.Reports.ViewOccupancyReports, Permissions.Reports.ViewFinancialReports),
        ["Pdf.OverdueReport"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Pdf.ViewContract"] = Staff(Permissions.Contracts.ExportPdf),
        ["Pdf.ViewReceipt"] = Staff(Permissions.Financials.ViewPayments),
        ["PermissionPackages.AssignToUser"] = Staff(Permissions.Users.ManagePermissions).WithRoles("SuperAdmin") with { Notes = "Only SuperAdmin may mutate delegation; Admin delegation is deliberately disabled." },
        ["PermissionPackages.Create"] = Staff(Permissions.Users.ManagePermissions).WithRoles("SuperAdmin") with { Notes = "Only SuperAdmin may mutate delegation; Admin delegation is deliberately disabled." },
        ["PermissionPackages.Delete"] = Staff(Permissions.Users.ManagePermissions).WithRoles("SuperAdmin") with { Notes = "Only SuperAdmin may mutate delegation; Admin delegation is deliberately disabled." },
        ["PermissionPackages.GetAll"] = Staff(Permissions.Users.ManagePermissions),
        ["PermissionPackages.GetById"] = Staff(Permissions.Users.ManagePermissions),
        ["PermissionPackages.GetModules"] = Staff(Permissions.Users.ManagePermissions),
        ["PermissionPackages.GetUserPackages"] = Staff(Permissions.Users.ManagePermissions),
        ["PermissionPackages.Update"] = Staff(Permissions.Users.ManagePermissions).WithRoles("SuperAdmin") with { Notes = "Only SuperAdmin may mutate delegation; Admin delegation is deliberately disabled." },
        ["ProtectedFiles.Download"] = Identity(IdentityPolicies.File) with { Notes = "Exact DB file record; matching owner tenant OR precise staff permission. Unregistered files denied." },
        ["PushSubscriptions.GetPublicKey"] = Public() with { Notes = "Public key only." },
        ["PushSubscriptions.Subscribe"] = Identity(IdentityPolicies.Self),
        ["PushSubscriptions.Unsubscribe"] = Identity(IdentityPolicies.Self),
        ["Refunds.Create"] = Staff(Permissions.Financials.CreateRefund) with { IsIdempotent = true },
        ["Refunds.Delete"] = Staff(Permissions.Financials.DeleteRefund) with { IsIdempotent = true },
        ["Refunds.GetAll"] = Staff(Permissions.Financials.ViewRefunds),
        ["Refunds.GetByContract"] = Staff(Permissions.Financials.ViewRefunds),
        ["Reports.GetDashboardStats"] = Staff(Permissions.Reports.ViewDashboard, Permissions.Reports.ViewFinancialReports) with { Notes = "Current DTO contains finance; BOTH keys required, no silent financial disclosure." },
        ["Reports.GetExpensesReport"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Reports.GetFinancialPerformance"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Reports.GetIncomeSummary"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Reports.GetOccupancyReport"] = Staff(Permissions.Reports.ViewOccupancyReports, Permissions.Reports.ViewFinancialReports) with { Notes = "Current DTO includes rent; BOTH keys required." },
        ["Reports.GetOverdueReport"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Reports.GetRevenueReport"] = Staff(Permissions.Reports.ViewFinancialReports),
        ["Reports.GetVisitorTraffic"] = Staff(Permissions.Reports.ViewVisitorTrafficReports),
        ["Settings.GetAll"] = Staff(Permissions.Settings.View),
        ["Settings.GetByGroup"] = Staff(Permissions.Settings.View),
        ["Settings.GetByKey"] = Staff(Permissions.Settings.View),
        ["Settings.GetSystemTime"] = Staff(Permissions.Settings.View),
        ["Settings.Reset"] = Staff(Permissions.Settings.Reset),
        ["Settings.ResetDatabase"] = Staff(Permissions.Settings.ResetDatabase),
        ["Settings.Update"] = Staff(Permissions.Settings.Edit),
        ["TenantAccounts.DepositAdvance"] = Staff(Permissions.Financials.DepositAdvance) with { IsIdempotent = true },
        ["TenantAccounts.GetAllBalances"] = Staff(Permissions.Tenants.ViewBalances),
        ["TenantAccounts.GetStatement"] = Staff(Permissions.Tenants.ViewStatement),
        ["TenantAccounts.GetWalletDeductions"] = Staff(Permissions.Financials.ViewPayments),
        ["TenantAccounts.ProcessMonthlyDues"] = Staff(Permissions.Financials.ProcessMonthlyDues) with { IsIdempotent = true },
        ["TenantComplaints.GetMyComplaints"] = Identity(IdentityPolicies.Tenant, "complaints"),
        ["TenantComplaints.Submit"] = Identity(IdentityPolicies.Tenant, "complaints"),
        ["TenantPortal.AcceptCharge"] = Identity(IdentityPolicies.Tenant, "maintenance") with { IsIdempotent = true },
        ["TenantPortal.CreateAccount"] = Staff(Permissions.Users.Create),
        ["TenantPortal.CreateStaff"] = Identity(IdentityPolicies.TenantOwner),
        ["TenantPortal.CreateVisitorPass"] = Identity(IdentityPolicies.Tenant, "visitors"),
        ["TenantPortal.GetContracts"] = Identity(IdentityPolicies.Tenant, "contracts"),
        ["TenantPortal.GetMaintenanceById"] = Identity(IdentityPolicies.Tenant, "maintenance"),
        ["TenantPortal.GetMyCirculars"] = Identity(IdentityPolicies.Tenant, "circulars"),
        ["TenantPortal.GetMyMaintenance"] = Identity(IdentityPolicies.Tenant, "maintenance"),
        ["TenantPortal.GetMyPendingCharges"] = Identity(IdentityPolicies.Tenant, "maintenance"),
        ["TenantPortal.GetMyStaff"] = Identity(IdentityPolicies.TenantOwner),
        ["TenantPortal.GetMyWalletHistory"] = Identity(IdentityPolicies.Tenant, "wallet"),
        ["TenantPortal.GetPayments"] = Identity(IdentityPolicies.Tenant, "payments"),
        ["TenantPortal.GetStatement"] = Identity(IdentityPolicies.Tenant, "statement"),
        ["TenantPortal.GetVisitorPasses"] = Identity(IdentityPolicies.Tenant, "visitors"),
        ["TenantPortal.RejectCharge"] = Identity(IdentityPolicies.Tenant, "maintenance") with { IsIdempotent = true },
        ["TenantPortal.RequestMaintenance"] = Identity(IdentityPolicies.Tenant, "maintenance"),
        ["TenantPortal.UploadReceipt"] = Identity(IdentityPolicies.Tenant, "payments") with { Notes = "tenantId must equal the live identity; upload <= 5 MiB." },
        ["Tenants.Create"] = Staff(Permissions.Tenants.Create),
        ["Tenants.Delete"] = Staff(Permissions.Tenants.Delete),
        ["Tenants.GetAll"] = Staff(Permissions.Tenants.View),
        ["Tenants.GetById"] = Staff(Permissions.Tenants.View),
        ["Tenants.GetContracts"] = Staff(Permissions.Contracts.View),
        ["Tenants.GetFinancialSummary"] = Staff(Permissions.Tenants.ViewBalances),
        ["Tenants.GetMaintenance"] = Staff(Permissions.Maintenance.View),
        ["Tenants.GetPayments"] = Staff(Permissions.Financials.ViewPayments),
        ["Tenants.GetPersonalInfo"] = Staff(Permissions.Tenants.View),
        ["Tenants.GetRentedUnits"] = Staff(Permissions.Units.View),
        ["Tenants.GetVisitorPasses"] = Staff(Permissions.Visitors.View),
        ["Tenants.Update"] = Staff(Permissions.Tenants.Edit),
        ["Units.Create"] = Staff(Permissions.Units.Create),
        ["Units.Delete"] = Staff(Permissions.Units.Delete),
        ["Units.GetAll"] = Staff(Permissions.Units.View),
        ["Units.GetById"] = Staff(Permissions.Units.View),
        ["Units.GetCountByStatus"] = Staff(Permissions.Units.View),
        ["Units.GetUnitHistory"] = Staff(Permissions.Units.ViewHistory),
        ["Units.Update"] = Staff(Permissions.Units.Edit),
        ["Users.AssignPermissions"] = Staff(Permissions.Users.ManagePermissions),
        ["Users.Create"] = Staff(Permissions.Users.Create),
        ["Users.Delete"] = Staff(Permissions.Users.Delete),
        ["Users.GetAll"] = Staff(Permissions.Users.View),
        ["Users.GetAllAvailablePermissions"] = Staff(Permissions.Users.ManagePermissions),
        ["Users.GetAuditLogs"] = Staff(Permissions.Users.ViewAuditLogs),
        ["Users.GetById"] = Staff(Permissions.Users.View),
        ["Users.GetUserPermissions"] = Staff(Permissions.Users.ManagePermissions),
        ["Users.GetUsersByTenant"] = Staff(Permissions.Users.View),
        ["Users.ReconcilePermissions"] = Staff(Permissions.Users.ManagePermissions) with { Notes = "Explicit legacy source review and full accounted decisions; target cannot be self." },
        ["Users.ResetPassword"] = Staff(Permissions.Users.ResetPassword),
        ["Users.ToggleLock"] = Staff(Permissions.Users.ToggleLock),
        ["Users.Update"] = Staff(Permissions.Users.Edit),
        ["VisitorBlacklist.Add"] = Staff(Permissions.Visitors.ManageBlacklist),
        ["VisitorBlacklist.Check"] = Staff(Permissions.Visitors.CheckBlacklist),
        ["VisitorBlacklist.GetAll"] = Staff(Permissions.Visitors.ManageBlacklist),
        ["VisitorBlacklist.GetById"] = Staff(Permissions.Visitors.ManageBlacklist),
        ["VisitorBlacklist.Remove"] = Staff(Permissions.Visitors.ManageBlacklist),
        ["VisitorPasses.Create"] = Staff(Permissions.Visitors.Create),
        ["VisitorPasses.GetAll"] = Staff(Permissions.Visitors.View),
        ["VisitorPasses.GetById"] = Staff(Permissions.Visitors.View),
        ["VisitorPasses.GetPaged"] = Staff(Permissions.Visitors.View),
        ["VisitorPasses.Revoke"] = Staff(Permissions.Visitors.RevokePass),
        ["VisitorWallet.AddBalance"] = Staff(Permissions.VisitorWallet.AddBalanceToPass) with { IsIdempotent = true },
        ["VisitorWallet.ChargeQr"] = Identity(IdentityPolicies.Tenant, "wallet") with { IsIdempotent = true, Notes = "Tenant from DB identity; UnitId belongs to an active contract; presented QR is a bearer purchase instrument." },
        ["VisitorWallet.GetAllShopsBalances"] = Staff(Permissions.VisitorWallet.ViewAllShopsBalances),
        ["VisitorWallet.GetGateCashReceipts"] = Staff(Permissions.VisitorWallet.ViewGateCashReport),
        ["VisitorWallet.GetGateCashReport"] = Staff(Permissions.VisitorWallet.ViewGateCashReport),
        ["VisitorWallet.GetGateShiftsReport"] = Staff(Permissions.VisitorWallet.ViewGateCashReport),
        ["VisitorWallet.GetMyShiftSummary"] = Staff(Permissions.VisitorWallet.ViewMyShiftSummary) with { Notes = "UserId from live identity only." },
        ["VisitorWallet.GetMyUnsettledBalance"] = Identity(IdentityPolicies.Tenant, "wallet"),
        ["VisitorWallet.GetTenantWalletHistoryForAdmin"] = Staff(Permissions.VisitorWallet.ViewTenantHistory),
        ["VisitorWallet.HandoverShift"] = Staff(Permissions.VisitorWallet.HandoverShift) with { IsIdempotent = true },
        ["VisitorWallet.IssuePaidPass"] = Staff(Permissions.VisitorWallet.IssuePaidPass) with { IsIdempotent = true },
        ["VisitorWallet.SettleShop"] = Staff(Permissions.VisitorWallet.SettleShopBalance) with { IsIdempotent = true },
        ["WeatherForecast.Get"] = Denied() with { Notes = "Demo endpoint disabled." },
    };
    public static string Key(ControllerActionDescriptor action) => $"{action.ControllerName}.{action.ActionName}";

    public static void AssertCoverage(IEnumerable<ControllerActionDescriptor> actions)
    {
        var actual = actions.Select(Key).ToHashSet(StringComparer.Ordinal);
        var missing = actual.Except(Rules.Keys).ToArray();
        var stale = Rules.Keys.Except(actual).ToArray();
        var unknown = Rules.Values.SelectMany(r => r.PermissionKeys).Where(k => !Permissions.IsKnown(k)).ToArray();
        if (missing.Length + stale.Length + unknown.Length != 0)
            throw new InvalidOperationException($"Endpoint authorization catalogue mismatch. Missing: {string.Join(',', missing)}; stale: {string.Join(',', stale)}; unknown keys: {string.Join(',', unknown)}");
    }
}

public sealed class EndpointSecurityConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        foreach (var action in controller.Actions)
        {
            var key = $"{controller.ControllerName}.{action.ActionName}";
            var rule = EndpointSecurityCatalog.Rules.GetValueOrDefault(key) ?? new(IdentityPolicies.Denied, Array.Empty<string>());
            foreach (var selector in action.Selectors)
            {
                // Anonymous is an explicit catalogue exception, not a way around classification.
                for (var i = selector.EndpointMetadata.Count - 1; i >= 0; i--)
                    if (selector.EndpointMetadata[i] is IAllowAnonymous) selector.EndpointMetadata.RemoveAt(i);
                selector.EndpointMetadata.Add(rule);
                if (rule.IsPublic) selector.EndpointMetadata.Add(new AllowAnonymousAttribute());
                else
                {
                    selector.EndpointMetadata.Add(new AuthorizeAttribute(rule.IdentityPolicy));
                    foreach (var permission in rule.PermissionKeys)
                        selector.EndpointMetadata.Add(new AuthorizeAttribute(permission));
                    if (rule.Roles is not null) selector.EndpointMetadata.Add(new AuthorizeAttribute { Roles = rule.Roles });
                }
            }
        }
    }
}

public sealed class EndpointSecurityStartupCheck(IActionDescriptorCollectionProvider actions) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        EndpointSecurityCatalog.AssertCoverage(actions.ActionDescriptors.Items.OfType<ControllerActionDescriptor>());
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

public static class PortalCapabilities
{
    public static readonly string[] All = { "statement", "contracts", "payments", "maintenance", "visitors", "wallet", "complaints", "circulars" };
}
