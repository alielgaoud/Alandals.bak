using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Settings;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class SettingsController : ControllerBase
    {
        private readonly ISettingService _settingService;

        public SettingsController(ISettingService settingService)
        {
            _settingService = settingService;
        }

        // GET: api/settings (كل الإعدادات مجمعة)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var groups = await _settingService.GetGroupedSettingsAsync();
            return Ok(ApiResponseDto<List<SettingGroupDto>>.SuccessResponse(groups));
        }

        // GET: api/settings/group/Numbering
        [HttpGet("group/{group}")]
        public async Task<IActionResult> GetByGroup(string group)
        {
            var settings = await _settingService.GetGroupAsync(group);
            return Ok(ApiResponseDto<Dictionary<string, string?>>.SuccessResponse(settings));
        }

        // GET: api/settings/key/Numbering.ContractFormat
        [HttpGet("key/{key}")]
        public async Task<IActionResult> GetByKey(string key)
        {
            var value = await _settingService.GetValueAsync(key);
            return Ok(ApiResponseDto<string?>.SuccessResponse(value));
        }

        // PUT: api/settings
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateSettingsDto dto)
        {
            string user = User.Identity?.Name ?? "Admin";
            foreach (var kv in dto.Values)
            {
                await _settingService.SetValueAsync(kv.Key, kv.Value, user);
            }
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حفظ الإعدادات بنجاح"));
        }

        // POST: api/settings/reset/Numbering.ContractFormat
        [HttpPost("reset/{key}")]
        public async Task<IActionResult> Reset(string key)
        {
            await _settingService.ResetToDefaultAsync(key);
            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إعادة الإعداد للقيمة الافتراضية"));
        }
    }
}