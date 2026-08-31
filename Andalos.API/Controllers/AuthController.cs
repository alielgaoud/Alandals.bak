using Andalos.API.DTOs.Auth;
using Andalos.API.DTOs.Common;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _auth.LoginAsync(dto);
                return Ok(ApiResponseDto<AuthResponseDto>.SuccessResponse(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponseDto<AuthResponseDto>.FailResponse(ex.Message));
            }
        }

        // 👈 الاندبوينت الجديد والآمن المخصص لتسجيل دخول المستأجرين من الويب
        [HttpPost("tenant-login")]
        public async Task<IActionResult> TenantLogin([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _auth.TenantLoginAsync(dto);
                return Ok(ApiResponseDto<TenantAuthResponseDto>.SuccessResponse(result, "تم تسجيل الدخول بنجاح"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponseDto<TenantAuthResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            try
            {
                var result = await _auth.RegisterAsync(dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}