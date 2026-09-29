namespace Andalos.API.Security;
public sealed class FinancialOperationGuard(CurrentUser current, EffectivePermissions permissions)
{
    public async Task RequireAsync(string key)
    {
        if (!current.Required.IsStaff || !await permissions.HasAsync(current.Required, key)) throw new ForbiddenOperationException();
    }
    public static void ValidateAmount(decimal amount, decimal max = 1_000_000m)
    {
        if (amount <= 0 || amount > max || decimal.Round(amount, 2) != amount) throw new ArgumentException("Amount must be positive, within the supported limit, and have at most 2 decimal places.");
    }
}
