using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Auth;
using Andalos.API.DTOs.Common;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly AppDbContext _db;
        private readonly IPermissionPackageService _permissionService;

        public AuthController(
            IAuthService auth,
            AppDbContext db,
            IPermissionPackageService permissionService)
        {
            _auth = auth;
            _db = db;
            _permissionService = permissionService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _auth.LoginAsync(dto);
                return Ok(
                    ApiResponseDto<AuthResponseDto>.SuccessResponse(
                        result, "تم تسجيل الدخول بنجاح"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(
                    ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        [HttpPost("tenant-login")]
        public async Task<IActionResult> TenantLogin([FromBody] LoginDto dto)
        {
            try
            {
                var result = await _auth.TenantLoginAsync(dto);
                return Ok(
                    ApiResponseDto<TenantAuthResponseDto>.SuccessResponse(
                        result, "تم تسجيل الدخول بنجاح"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(
                    ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // الواجهة تستدعي بالضبط:
        // GET /api/Auth/me/permissions
        [Authorize(Roles = "SuperAdmin,Admin,Accountant,GateKeeper")]
        [HttpGet("me/permissions")]
        public async Task<IActionResult> GetMyPermissions()
        {
            var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(rawId, out var userId))
            {
                return Unauthorized(
                    ApiResponseDto<string>.FailResponse("هوية الجلسة غير صالحة"));
            }

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    u => u.Id == userId && u.IsActive,
                    HttpContext.RequestAborted);

            if (user == null || user.IsLocked)
            {
                return Unauthorized(
                    ApiResponseDto<string>.FailResponse(
                        "الحساب غير متاح؛ يرجى تسجيل الدخول مجدداً"));
            }

            // إذا تغيّر الدور في قاعدة البيانات، لا نعتمد الدور القديم في JWT.
            if (!User.IsInRole(user.Role.ToString()))
            {
                return Unauthorized(
                    ApiResponseDto<string>.FailResponse(
                        "تغيّر دور الحساب؛ يرجى تسجيل الدخول مجدداً"));
            }

            if (user.Role != UserRole.SuperAdmin &&
                user.Role != UserRole.Admin &&
                user.Role != UserRole.Accountant &&
                user.Role != UserRole.GateKeeper)
            {
                return Forbid();
            }

            List<string> permissions;

            if (user.Role == UserRole.SuperAdmin)
            {
                permissions = Permissions.GetAllPermissions();
            }
            else
            {
                permissions =
                    await _permissionService
                        .GetEffectivePermissionsForUserAsync(user.Id);
            }

            permissions = permissions
                .Distinct(StringComparer.Ordinal)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            // Modules للعرض فقط؛ لا تُستخدم بدلاً من مفاتيح Module.Action.
            var modules = PermissionModules.ModuleMap
                .Where(module =>
                    module.Value.Any(key => permissions.Contains(key)))
                .Select(module => module.Key)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            var result = new CurrentPermissionsDto
            {
                Role = user.Role.ToString(),
                Permissions = permissions,
                Modules = modules
            };

            return Ok(
                ApiResponseDto<CurrentPermissionsDto>.SuccessResponse(
                    result, "تم تحديث الصلاحيات"));
        }

        // لا تترك RegisterDto.Role متاحاً في تسجيل عام غير محمي.
        [HttpPost("register")]
        public IActionResult Register()
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                ApiResponseDto<string>.FailResponse(
                    "التسجيل العام معطل؛ تُنشأ الحسابات من إدارة المستخدمين"));
        }
    }
}