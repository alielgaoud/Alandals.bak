using Andalos.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Andalos.API.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;

        public JwtHelper(IConfiguration config)
        {
            _config = config;
        }

        // للإبقاء على استدعاءات أخرى للدالة القديمة إن وُجدت.
        public string GenerateToken(
            User user,
            List<string> permissions)
        {
            return GenerateToken(
                user, permissions, out _);
        }

        public string GenerateToken(
            User user,
            List<string> permissions,
            out DateTime expirationUtc)
        {
            var settings = _config.GetSection("JwtSettings");

            var secret = settings["SecretKey"]
                ?? throw new InvalidOperationException(
                    "JwtSettings:SecretKey is missing");

            if (!double.TryParse(
                    settings["ExpiryMinutes"],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var minutes) ||
                !double.IsFinite(minutes) ||
                minutes <= 0)
            {
                throw new InvalidOperationException(
                    "JwtSettings:ExpiryMinutes must be positive");
            }

            var issuedAtUtc = DateTime.UtcNow;
            expirationUtc = issuedAtUtc.AddMinutes(minutes);

            var signingKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secret));

            var credentials = new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),
                new Claim(
                    ClaimTypes.Name,
                    user.UserName),
                new Claim(
                    ClaimTypes.Role,
                    user.Role.ToString()),
                new Claim(
                    "FullName",
                    user.FullName)
            };

            if (user.TenantId.HasValue)
            {
                claims.Add(
                    new Claim(
                        "TenantId",
                        user.TenantId.Value.ToString()));
            }

            // بيانات JWT لقطة وقت الدخول؛ المعالج أعلاه
            // يتحقق من المنح الحالية في قاعدة البيانات.
            foreach (var permission in permissions)
            {
                claims.Add(
                    new Claim("Permission", permission));
            }

            var jwt = new JwtSecurityToken(
                issuer: settings["Issuer"],
                audience: settings["Audience"],
                claims: claims,
                notBefore: issuedAtUtc,
                expires: expirationUtc,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(jwt);
        }
    }
}