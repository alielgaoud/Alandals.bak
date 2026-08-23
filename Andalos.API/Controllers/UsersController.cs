using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Users;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: api/users
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllAsync();
            return Ok(ApiResponseDto<List<UserResponseDto>>.SuccessResponse(users));
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponseDto<UserResponseDto>.FailResponse("المستخدم غير موجود"));

            return Ok(ApiResponseDto<UserResponseDto>.SuccessResponse(user));
        }

        // POST: api/users
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserByAdminDto dto)
        {
            try
            {
                var user = await _userService.CreateUserAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = user.Id },
                    ApiResponseDto<UserResponseDto>.SuccessResponse(user, "تم إنشاء حساب المستخدم بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<UserResponseDto>.FailResponse(ex.Message));
            }
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var user = await _userService.UpdateUserAsync(id, dto);
                if (user == null)
                    return NotFound(ApiResponseDto<UserResponseDto>.FailResponse("المستخدم غير موجود"));

                return Ok(ApiResponseDto<UserResponseDto>.SuccessResponse(user, "تم تحديث بيانات المستخدم بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<UserResponseDto>.FailResponse(ex.Message));
            }
        }

        // PUT: api/users/5/reset-password
        [HttpPut("{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] AdminResetPasswordDto dto)
        {
            var result = await _userService.ResetPasswordAsync(id, dto.NewPassword);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("المستخدم غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إعادة تعيين كلمة المرور بنجاح"));
        }

        // PUT: api/users/5/toggle-lock
        [HttpPut("{id}/toggle-lock")]
        public async Task<IActionResult> ToggleLock(int id, [FromQuery] bool isLocked)
        {
            var result = await _userService.ToggleLockAccountAsync(id, isLocked);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("المستخدم غير موجود"));

            string msg = isLocked ? "تم قفل الحساب بنجاح" : "تم إلغاء قفل الحساب بنجاح";
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, msg));
        }

        // DELETE: api/users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _userService.DeleteUserAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("المستخدم غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف المستخدم بنجاح"));
        }
    }
}