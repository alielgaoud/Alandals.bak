using Andalos.API.Authorization;
using Andalos.API.Constants;
using Andalos.API.DTOs.Circulars;
using Andalos.API.DTOs.Common;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CircularsController : ControllerBase
    {
        private readonly ICircularService _service;

        public CircularsController(ICircularService service)
        {
            _service = service;
        }

        private string CurrentUserName =>
            User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.Identity?.Name
            ?? "الإدارة";

        [HttpGet]
        [HasPermission(Permissions.Circulars.View)]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            return Ok(ApiResponseDto<List<CircularResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("{id}")]
        [HasPermission(Permissions.Circulars.View)]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null)
                return NotFound(ApiResponseDto<CircularResponseDto>.FailResponse("التعميم غير موجود"));

            return Ok(ApiResponseDto<CircularResponseDto>.SuccessResponse(item));
        }

        [HttpPost]
        [HasPermission(Permissions.Circulars.Create)]
        public async Task<IActionResult> Create([FromBody] CreateCircularDto dto)
        {
            var created = await _service.CreateAsync(dto, CurrentUserName);
            return Ok(ApiResponseDto<CircularResponseDto>.SuccessResponse(created, "تم نشر التعميم بنجاح"));
        }

        [HttpPut("{id}")]
        [HasPermission(Permissions.Circulars.Edit)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateCircularDto dto)
        {
            try
            {
                var updated = await _service.UpdateAsync(id, dto, CurrentUserName);
                return Ok(ApiResponseDto<CircularResponseDto>.SuccessResponse(updated, "تم تعديل التعميم بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<CircularResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [HasPermission(Permissions.Circulars.Delete)]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _service.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("التعميم غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف التعميم بنجاح"));
        }
    }
}