using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VisitorPassesController : ControllerBase
    {
        private readonly IVisitorPassService _passService;

        public VisitorPassesController(IVisitorPassService passService)
        {
            _passService = passService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] DateTime? date, [FromQuery] int? unitId)
        {
            var list = await _passService.GetAllAsync(date, unitId);
            return Ok(ApiResponseDto<List<VisitorPassResponseDto>>.SuccessResponse(list));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var pass = await _passService.GetByIdAsync(id);
            if (pass == null)
                return NotFound(ApiResponseDto<VisitorPassResponseDto>.FailResponse("التصريح غير موجود"));

            return Ok(ApiResponseDto<VisitorPassResponseDto>.SuccessResponse(pass));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateVisitorPassDto dto)
        {
            try
            {
                string user = User.Identity?.Name ?? "Admin";
                var result = await _passService.CreatePassAsync(dto, user);
                return CreatedAtAction(nameof(GetById), new { id = result.Id },
                    ApiResponseDto<VisitorPassResponseDto>.SuccessResponse(result, "تم إنشاء تصريح الدخول بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<VisitorPassResponseDto>.FailResponse(ex.Message));
            }
        }

        [HttpPut("{id}/revoke")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Revoke(int id)
        {
            var result = await _passService.RevokePassAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("التصريح غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إبطال تصريح الدخول بنجاح"));
        }
    }
}