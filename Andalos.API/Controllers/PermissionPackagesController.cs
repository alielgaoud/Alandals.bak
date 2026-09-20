using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Users;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class PermissionPackagesController : ControllerBase
    {
        private readonly IPermissionPackageService _service;

        public PermissionPackagesController(IPermissionPackageService service)
        {
            _service = service;
        }

        // قائمة الأقسام المبسطة للفرونت
        [HttpGet("modules")]
        public async Task<IActionResult> GetModules()
        {
            var list = await _service.GetModulesAsync();
            return Ok(ApiResponseDto<List<PermissionModuleDto>>.SuccessResponse(list));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(ApiResponseDto<List<PermissionPackageResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null)
                return NotFound(ApiResponseDto<string>.FailResponse("الصلاحية غير موجودة"));

            return Ok(ApiResponseDto<PermissionPackageResponseDto>.SuccessResponse(item));
        }

        // إنشاء صلاحية بمسمى حر + أقسام
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePermissionPackageDto dto)
        {
            try
            {
                var result = await _service.CreateAsync(dto);
                return Ok(ApiResponseDto<PermissionPackageResponseDto>.SuccessResponse(result, "تم إنشاء الصلاحية بنجاح"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePermissionPackageDto dto)
        {
            try
            {
                var result = await _service.UpdateAsync(id, dto);
                if (result == null)
                    return NotFound(ApiResponseDto<string>.FailResponse("الصلاحية غير موجودة"));

                return Ok(ApiResponseDto<PermissionPackageResponseDto>.SuccessResponse(result, "تم تحديث الصلاحية بنجاح"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            if (!ok) return NotFound(ApiResponseDto<bool>.FailResponse("الصلاحية غير موجودة"));
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الصلاحية"));
        }

        // تعيين باقات لموظف
        [HttpPost("assign-to-user")]
        public async Task<IActionResult> AssignToUser([FromBody] AssignPackagesToUserDto dto)
        {
            try
            {
                var ok = await _service.AssignPackagesToUserAsync(dto);
                if (!ok) return NotFound(ApiResponseDto<bool>.FailResponse("المستخدم غير موجود"));
                return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعيين الصلاحيات للمستخدم بنجاح"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserPackages(int userId)
        {
            var list = await _service.GetUserPackagesAsync(userId);
            return Ok(ApiResponseDto<List<PermissionPackageResponseDto>>.SuccessResponse(list));
        }
    }
}