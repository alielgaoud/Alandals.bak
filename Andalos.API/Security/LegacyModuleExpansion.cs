using Andalos.API.Constants;
namespace Andalos.API.Security;
public static class LegacyModuleExpansion
{
    // Frozen pre-upgrade module expansion. New dangerous actions are never implicitly added to legacy module requests.
    private static readonly string[] Keys =
    {
        "Units.View",
        "Units.Create",
        "Units.Edit",
        "Units.Delete",
        "Units.ViewHistory",
        "Tenants.View",
        "Tenants.Create",
        "Tenants.Edit",
        "Tenants.Delete",
        "Tenants.ViewStatement",
        "Tenants.ViewBalances",
        "Contracts.View",
        "Contracts.Create",
        "Contracts.Edit",
        "Contracts.Renew",
        "Contracts.UpdateStatus",
        "Contracts.Delete",
        "Contracts.ExportPdf",
        "Financials.ViewPayments",
        "Financials.CreatePayment",
        "Financials.DepositAdvance",
        "Financials.ProcessMonthlyDues",
        "Financials.ViewRefunds",
        "Financials.CreateRefund",
        "Expenses.View",
        "Expenses.Create",
        "Expenses.Delete",
        "Demands.GeneratePdf",
        "Demands.SendToTenant",
        "Complaints.View",
        "Complaints.Reply",
        "Complaints.UpdateStatus",
        "Complaints.ExportPdf",
        "BankTransfers.View",
        "BankTransfers.Review",
        "VisitorWallet.ViewAllShopsBalances",
        "VisitorWallet.SettleShopBalance",
        "VisitorWallet.HandoverShift",
        "VisitorWallet.ViewGateCashReport",
        "Visitors.View",
        "Visitors.RevokePass",
        "Visitors.ManageBlacklist",
        "Maintenance.View",
        "Maintenance.Create",
        "Maintenance.EditStatus",
        "Reports.ViewDashboard",
        "Reports.ViewFinancialReports",
        "Reports.ViewOccupancyReports",
        "Settings.View",
        "Settings.Edit",
        "Users.View",
        "Users.Create",
        "Users.Edit",
        "Users.Delete",
        "Users.ResetPassword",
        "Users.ToggleLock",
        "Users.ManagePermissions",
        "Circulars.View",
        "Circulars.Create",
        "Circulars.Edit",
        "Circulars.Delete",
    };
    public static List<string> Expand(IEnumerable<string> modules)
    {
        var list = modules.Distinct(StringComparer.Ordinal).ToArray();
        if (list.Any(m => !PermissionModules.ModuleMap.ContainsKey(m))) throw new ArgumentException("Unknown module.");
        return Keys.Where(k => list.Contains(k.Split('.')[0], StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }
}
