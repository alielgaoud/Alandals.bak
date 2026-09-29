using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Auth;
using Andalos.API.DTOs.Common;
using Andalos.API.Interfaces;
using Andalos.API.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace Andalos.API.Controllers;
[ApiController, Route("api/[controller]")]
public sealed class AuthController(IAuthService auth, CurrentUser current, EffectivePermissions permissions,
    AppDbContext db, PasswordService passwords) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        try { return Ok(ApiResponseDto<AuthResponseDto>.SuccessResponse(await auth.LoginAsync(dto))); }
        catch (UnauthorizedAccessException) { return Unauthorized(ApiResponseDto<AuthResponseDto>.FailResponse("بيانات الدخول غير صحيحة أو الحساب غير متاح.")); }
    }
    [HttpPost("tenant-login"), EnableRateLimiting("auth-login")]
    public async Task<IActionResult> TenantLogin(LoginDto dto)
    {
        try { return Ok(ApiResponseDto<TenantAuthResponseDto>.SuccessResponse(await auth.TenantLoginAsync(dto))); }
        catch (UnauthorizedAccessException) { return Unauthorized(ApiResponseDto<TenantAuthResponseDto>.FailResponse("بيانات الدخول غير صحيحة أو الحساب غير متاح.")); }
    }
    [HttpPost("register")]
    public IActionResult Register(RegisterDto dto) => StatusCode(StatusCodes.Status410Gone,
        ApiResponseDto<object>.FailResponse("التسجيل العام معطل. إنشاء الحسابات يتم بواسطة مسؤول مخول."));

    [HttpGet("me/permissions")]
    public async Task<IActionResult> MePermissions()
    {
        var user = current.Required;
        if (!user.IsStaff) throw new ForbiddenOperationException();
        var keys = await permissions.GetAsync(user, HttpContext.RequestAborted);
        Response.Headers.CacheControl = "no-store";
        return Ok(ApiResponseDto<CurrentPermissionsDto>.SuccessResponse(new() { Role = user.Role.ToString(),
            Permissions = keys.ToList(), Modules = Permissions.ModulesFor(keys), PermissionsVersion = user.PermissionsVersion,
            ReconciliationRequired = !user.PermissionsReconciled }));
    }
    [HttpPut("me/password"), EnableRateLimiting("auth-login")]
    public async Task<IActionResult> ChangePassword(ChangeOwnPasswordDto dto)
    {
        var u = await db.Users.SingleAsync(u => u.Id == current.UserId);
        if (!passwords.Verify(u, dto.CurrentPassword, out _)) throw new ForbiddenOperationException();
        u.PasswordHash = passwords.Hash(u, dto.NewPassword); u.RequiresPasswordChange = false;
        await db.SaveChangesAsync(); // invalidates all old JWTs. Client must login again.
        return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تغيير كلمة المرور؛ يرجى تسجيل الدخول مجدداً."));
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var u = await db.Users.SingleAsync(u => u.Id == current.UserId);
        u.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(); // all sessions, explicitly documented
        return Ok(ApiResponseDto<bool>.SuccessResponse(true));
    }
}
