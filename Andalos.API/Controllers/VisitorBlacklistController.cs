using Andalos.API.DTOs.Blacklist;
using Andalos.API.DTOs.Common;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class VisitorBlacklistController : ControllerBase
    {
        private readonly IVisitorBlacklistService _blacklistService;

        public VisitorBlacklistController(IVisitorBlacklistService blacklistService)
        {
            _blacklistService = blacklistService;
        }

        // GET: api/VisitorBlacklist
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _blacklistService.GetAllAsync();
            return Ok(ApiResponseDto<List<BlacklistResponseDto>>.SuccessResponse(list));
        }

        // GET: api/VisitorBlacklist/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var item = await _blacklistService.GetByIdAsync(id);
            if (item == null)
                return NotFound(ApiResponseDto<BlacklistResponseDto>.FailResponse("السجل غير موجود"));

            return Ok(ApiResponseDto<BlacklistResponseDto>.SuccessResponse(item));
        }

        // POST: api/VisitorBlacklist (رفع صورة + Form)
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] CreateBlacklistDto dto)
        {
            try
            {
                string user = User.Identity?.Name ?? "Admin";
                var result = await _blacklistService.AddAsync(dto, user);
                return CreatedAtAction(nameof(GetById), new { id = result.Id },
                    ApiResponseDto<BlacklistResponseDto>.SuccessResponse(result, "تمت إضافة الزائر إلى القائمة السوداء بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<BlacklistResponseDto>.FailResponse(ex.Message));
            }
        }

        // DELETE: api/VisitorBlacklist/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Remove(int id)
        {
            var result = await _blacklistService.RemoveAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("السجل غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم رفع الحظر عن الزائر بنجاح"));
        }

        // GET: api/VisitorBlacklist/check (فحص أمني سريع ومفتوح للبوابة والإنشاء)
        [HttpGet("check")]
        [AllowAnonymous]
        public async Task<IActionResult> Check(
            [FromQuery] string? phone,
            [FromQuery] string? nationalId,
            [FromQuery] string? fullName)
        {
            var result = await _blacklistService.CheckVisitorAsync(phone, nationalId, fullName);
            return Ok(ApiResponseDto<CheckBlacklistResultDto>.SuccessResponse(result));
        }
    }
}