namespace Andalos.API.Security;
public static class PushSubscriptionSecurity
{
    public static bool IsAllowedEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Port == 443 &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
        (uri.Host is "fcm.googleapis.com" or "updates.push.services.mozilla.com" or "web.push.apple.com" ||
         uri.Host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase));
    public static void ValidateEndpoint(string endpoint)
    {
        if (!IsAllowedEndpoint(endpoint)) throw new ArgumentException("Unsupported browser push endpoint.");
    }
}
