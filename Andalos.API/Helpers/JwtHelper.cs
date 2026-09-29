using Andalos.API.Models;
using Microsoft.IdentityModel.Tokens;
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

        // 👈 إضافة معامل الصلاحيات للدالة لزرعها داخل التوكن
        public string GenerateToken(User user, List<string> permissions)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new ArgumentNullException("Jwt SecretKey is missing");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("FullName", user.FullName) // أضفنا الاسم الكامل للتوكن
            };

            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim("TenantId", user.TenantId.Value.ToString()));
            }

            // 👈 زراعة الصلاحيات بداخل الـ JWT Claims تحت اسم "Permission"
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("Permission", permission));
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiryMinutes"] ?? "1440")), // افتراضي يوم كامل
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}