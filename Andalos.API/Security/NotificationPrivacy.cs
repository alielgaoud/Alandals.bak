using Andalos.API.Constants;
using Andalos.API.Enums;
namespace Andalos.API.Security;
public static class NotificationPrivacy
{
    public static string? StaffKey(NotificationType type) => type switch
    {
        NotificationType.General or NotificationType.System => null,
        NotificationType.NewComplaint or NotificationType.ComplaintReply or NotificationType.ComplaintStatusChanged => Permissions.Complaints.View,
        NotificationType.NewBankTransfer or NotificationType.BankTransferApproved or NotificationType.BankTransferRejected => Permissions.BankTransfers.View,
        NotificationType.ContractExpiringSoon or NotificationType.ContractRenewed or NotificationType.ContractTerminated => Permissions.Contracts.View,
        NotificationType.PaymentReminder or NotificationType.PaymentOverdue or NotificationType.AutomaticDeduction or NotificationType.PaymentReceived => Permissions.Financials.ViewPayments,
        NotificationType.NewMaintenanceRequest or NotificationType.MaintenanceStatusChanged => Permissions.Maintenance.View,
        NotificationType.MaintenanceChargeOffer or NotificationType.MaintenanceChargeApproved or NotificationType.MaintenanceChargeRejected => Permissions.Financials.ViewCharges,
        NotificationType.VisitorRejected or NotificationType.VisitorEntered => Permissions.Gate.ViewLogs,
        NotificationType.NewCircular => Permissions.Circulars.View,
        _ => "unknown"
    };
    public static string? PortalCapability(NotificationType type) => type switch
    {
        NotificationType.General or NotificationType.System => null,
        NotificationType.NewComplaint or NotificationType.ComplaintReply or NotificationType.ComplaintStatusChanged => "complaints",
        NotificationType.NewBankTransfer or NotificationType.BankTransferApproved or NotificationType.BankTransferRejected or
        NotificationType.PaymentReminder or NotificationType.PaymentOverdue or NotificationType.AutomaticDeduction or NotificationType.PaymentReceived => "payments",
        NotificationType.ContractExpiringSoon or NotificationType.ContractRenewed or NotificationType.ContractTerminated => "contracts",
        NotificationType.NewMaintenanceRequest or NotificationType.MaintenanceStatusChanged or NotificationType.MaintenanceChargeOffer or
        NotificationType.MaintenanceChargeApproved or NotificationType.MaintenanceChargeRejected => "maintenance",
        NotificationType.VisitorRejected or NotificationType.VisitorEntered => "visitors",
        NotificationType.NewCircular => "circulars",
        _ => "unknown"
    };
}
