using Andalos.API.DTOs.Auth;

namespace Andalos.API.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
        Task<TenantAuthResponseDto> TenantLoginAsync(LoginDto dto);
    }
}