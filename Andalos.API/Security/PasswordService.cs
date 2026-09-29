using Andalos.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
namespace Andalos.API.Security;
public sealed class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));
    private readonly User _dummy = new();
    private string? _dummyHash;
    public void VerifyDummy(string password)
    {
        _dummyHash ??= _hasher.HashPassword(_dummy, "NotAnAccountPassword-OnlyTiming");
        _hasher.VerifyHashedPassword(_dummy, _dummyHash, password.Length <= 128 ? password : "invalid");
    }
    public static void ValidateNew(string password)
    {
        if (password.Length < 12 || password.Length > 128 || string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password must contain between 12 and 128 characters.");
    }
    public string Hash(User user, string password)
    {
        ValidateNew(password);
        return _hasher.HashPassword(user, password);
    }
    public bool Verify(User user, string password, out bool rehash)
    {
        rehash = false;
        if (password.Length > 128) return false;
        // Legacy SHA256 upgrade only after a successful, fixed-time comparison.
        if (user.PasswordHash.Length == 44)
        {
            try
            {
                var ok = CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(user.PasswordHash), SHA256.HashData(Encoding.UTF8.GetBytes(password)));
                rehash = ok;
                return ok;
            }
            catch (FormatException) { return false; }
        }
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        rehash = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result != PasswordVerificationResult.Failed;
    }
    public string Upgrade(User user, string password) => _hasher.HashPassword(user, password); // May upgrade a valid old shorter password.
}
