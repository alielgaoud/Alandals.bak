using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Contracts;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ContractsController : ControllerBase
    {
        private readonly IContractService _contractService;

        public ContractsController(IContractService contractService)
        {
            _contractService = contractService;
        }

        // GET: api/contracts
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var contracts = await _contractService.GetAllAsync();
            return Ok(ApiResponseDto<List<ContractResponseDto>>.SuccessResponse(contracts));
        }

        // GET: api/contracts/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null)
                return NotFound(ApiResponseDto<ContractResponseDto>.FailResponse("العقد غير موجود"));

            return Ok(ApiResponseDto<ContractResponseDto>.SuccessResponse(contract));
        }

        // POST: api/contracts
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateContractDto dto)
        {
            try
            {
                var contract = await _contractService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = contract.Id },
                    ApiResponseDto<ContractResponseDto>.SuccessResponse(contract, "تم إنشاء العقد وتفعيل حجز المحل بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<ContractResponseDto>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<ContractResponseDto>.FailResponse(ex.Message));
            }
        }

        // PUT: api/contracts/5/status
        [HttpPut("{id}/status")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] ContractStatus status)
        {
            var result = await _contractService.UpdateStatusAsync(id, status);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("حدث خطأ أثناء تعديل حالة العقد"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم تعديل حالة العقد وتحديث حالة المحل بنجاح"));
        }

        // DELETE: api/contracts/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _contractService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("العقد غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم إلغاء العقد وإخلاء المحل بنجاح"));
        }
    }
}