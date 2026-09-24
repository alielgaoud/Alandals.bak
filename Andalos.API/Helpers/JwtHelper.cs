using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Andalos.API.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;
        private readonly IServiceProvider _serviceProvider;

        public JwtHelper(IConfiguration config, IServiceProvider serviceProvider)
        {
            _config = config;
            _serviceProvider = serviceProvider;
        }

        public string GenerateToken(User user)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? throw new ArgumentNullException("Jwt SecretKey is missing");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            if (user.TenantId.HasValue)
            {
                claims.Add(new Claim("TenantId", user.TenantId.Value.ToString()));
            }

            // قراءة مهلة الجلسة من إعدادات النظام - مترابطة
            double expiryMinutes = Convert.ToDouble(jwtSettings["ExpiryMinutes"] ?? "60");
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
                var sessionSetting = db.Settings.AsNoTracking().FirstOrDefault(s => s.SettingKey == Constants.SettingKeys.SystemSessionTimeout);
                if (sessionSetting != null && int.TryParse(sessionSetting.SettingValue, out var sessionTimeout) && sessionTimeout > 0)
                {
                    expiryMinutes = sessionTimeout;
                }
            }
            catch
            {
                // في حال فشل قراءة الإعدادات، نستخدم القيمة من appsettings
            }

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}