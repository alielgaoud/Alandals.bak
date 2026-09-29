using Andalos.API.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
namespace Andalos.API.Helpers;
public sealed class JwtHelper(IConfiguration configuration)
{
    public string GenerateToken(User user) => GenerateToken(user, out _);
    public string GenerateToken(User user, out DateTime expiration)
    {
        var settings = configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["SecretKey"] ?? throw new InvalidOperationException("JWT signing key is required.")));
        var now = DateTime.UtcNow;
        expiration = now.AddMinutes(settings.GetValue<int>("ExpiryMinutes", 60));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString()), new("security_stamp", user.SecurityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        if ((user.Role is Andalos.API.Enums.UserRole.Tenant or Andalos.API.Enums.UserRole.TenantStaff) && user.TenantId.HasValue)
            claims.Add(new("TenantId", user.TenantId.Value.ToString()));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(settings["Issuer"], settings["Audience"], claims,
            notBefore: now, expires: expiration, signingCredentials: new(key, SecurityAlgorithms.HmacSha256)));
    }
}
