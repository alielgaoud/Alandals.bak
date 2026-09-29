using Andalos.API.Models;
namespace Andalos.API.Security;
public static class ConcurrencyEntities
{
    public static readonly Type[] Types = { typeof(User), typeof(PermissionPackage), typeof(Tenant),
        typeof(VisitorPass), typeof(GatekeeperShift), typeof(TenantCharge), typeof(BankTransferRequest),
        typeof(Payment), typeof(Refund), typeof(MaintenanceRequest), typeof(PassTransaction), typeof(NumberSequence), typeof(Contract) };
}
