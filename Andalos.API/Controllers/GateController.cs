using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,Admin,GateKeeper")]
    public class GateController : ControllerBase
    {
        private readonly IVisitorPassService _passService;

        public GateController(IVisitorPassService passService)
        {
            _passService = passService;
        }

        // مسح الباركود والتحقق الفوري
        [HttpPost("scan")]
        public async Task<IActionResult> Scan([FromBody] ScanPassDto dto)
        {
            string scannedBy = User.Identity?.Name ?? "حارس البوابة";
            var result = await _passService.ScanAndValidatePassAsync(dto, scannedBy);
            return Ok(ApiResponseDto<ScanResultDto>.SuccessResponse(result));
        }

        // سجل الدخول لليوم أو لتاريخ محدد
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs([FromQuery] DateTime? date)
        {
            var logs = await _passService.GetEntryLogsAsync(date ?? DateTime.Today);
            return Ok(ApiResponseDto<List<EntryLogResponseDto>>.SuccessResponse(logs));
        }
    }
}