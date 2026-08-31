namespace Andalos.API.DTOs.Auth
{
    public class TenantAuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = "Tenant";
        public int TenantId { get; set; } // مهم جداً للأنجولر
        public DateTime Expiration { get; set; }
    }
}