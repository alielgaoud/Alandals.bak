using Andalos.API.Models;
namespace Andalos.API.Security;
public static class AuditRedaction
{
    private static readonly HashSet<string> Secrets = new(StringComparer.OrdinalIgnoreCase)
    { "PasswordHash", "SecurityStamp", "Revision", "P256dh", "Auth", "Endpoint", "PassCode", "NationalId", "Phone", "VisitorPhone" };
    public static bool IsSecretSetting(string key) => key.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Secret", StringComparison.OrdinalIgnoreCase) || key.Contains("Password", StringComparison.OrdinalIgnoreCase);
    public static bool IsSensitive(object entity, string name) => Secrets.Contains(name) ||
        (entity is Setting s && IsSecretSetting(s.SettingKey) && name is "SettingValue" or "DefaultValue");
}
