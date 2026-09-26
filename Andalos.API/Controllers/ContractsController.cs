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

        // PUT: api/contracts/5  - تعديل شامل للعقد
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateContractDto dto)
        {
            try
            {
                var contract = await _contractService.UpdateAsync(id, dto);
                return Ok(ApiResponseDto<ContractResponseDto>.SuccessResponse(contract, "تم تعديل العقد بنجاح مع تحديث جميع البنود والرسوم"));
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

        [HttpPost("{id}/renew")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Renew(int id, [FromBody] RenewContractDto dto)
        {
            try
            {
                var result = await _contractService.RenewAsync(id, dto);
                return Ok(ApiResponseDto<ContractResponseDto>.SuccessResponse(result, "تم تجديد العقد واحتساب الزيادة السنوية بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<string>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // ===== Endpoints جديدة لزيادة الترابط =====

        // GET: api/contracts/by-tenant/5
        [HttpGet("by-tenant/{tenantId}")]
        public async Task<IActionResult> GetByTenant(int tenantId)
        {
            var contracts = await _contractService.GetByTenantAsync(tenantId);
            return Ok(ApiResponseDto<List<ContractResponseDto>>.SuccessResponse(contracts));
        }

        // GET: api/contracts/by-unit/5
        [HttpGet("by-unit/{unitId}")]
        public async Task<IActionResult> GetByUnit(int unitId)
        {
            var contracts = await _contractService.GetByUnitAsync(unitId);
            return Ok(ApiResponseDto<List<ContractResponseDto>>.SuccessResponse(contracts));
        }

        // GET: api/contracts/expiring?days=30
        [HttpGet("expiring")]
        public async Task<IActionResult> GetExpiring([FromQuery] int days = 30)
        {
            var contracts = await _contractService.GetExpiringAsync(days);
            return Ok(ApiResponseDto<List<ContractResponseDto>>.SuccessResponse(contracts));
        }

        // GET: api/contracts/5/renewal-chain
        [HttpGet("{id}/renewal-chain")]
        public async Task<IActionResult> GetRenewalChain(int id)
        {
            var chain = await _contractService.GetRenewalChainAsync(id);
            if (chain == null)
                return NotFound(ApiResponseDto<ContractRenewalChainDto>.FailResponse("العقد غير موجود"));

            return Ok(ApiResponseDto<ContractRenewalChainDto>.SuccessResponse(chain));
        }

        // GET: api/contracts/5/financial-summary
        [HttpGet("{id}/financial-summary")]
        public async Task<IActionResult> GetFinancialSummary(int id)
        {
            var summary = await _contractService.GetFinancialSummaryAsync(id);
            if (summary == null)
                return NotFound(ApiResponseDto<ContractFinancialSummaryDto>.FailResponse("العقد غير موجود"));

            return Ok(ApiResponseDto<ContractFinancialSummaryDto>.SuccessResponse(summary));
        }

        // ===== المجموعة المالية الجديدة =====

        // GET: api/contracts/5/payments
        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetPayments(int id)
        {
            try
            {
                var payments = await _contractService.GetPaymentsAsync(id);
                return Ok(ApiResponseDto<List<DTOs.Payments.PaymentResponseDto>>.SuccessResponse(payments));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }

        // GET: api/contracts/5/statement?fromDate&toDate
        [HttpGet("{id}/statement")]
        public async Task<IActionResult> GetStatement(int id, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var statement = await _contractService.GetStatementAsync(id, fromDate, toDate);
            if (statement == null)
                return NotFound(ApiResponseDto<ContractStatementDto>.FailResponse("العقد غير موجود"));

            return Ok(ApiResponseDto<ContractStatementDto>.SuccessResponse(statement));
        }

        // POST: api/contracts/5/fees
        [HttpPost("{id}/fees")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> AddFee(int id, [FromBody] CreateContractFeeDto dto)
        {
            try
            {
                var fee = await _contractService.AddFeeAsync(id, dto);
                return Ok(ApiResponseDto<ContractFeeResponseDto>.SuccessResponse(fee, "تمت إضافة الرسم بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<ContractFeeResponseDto>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<ContractFeeResponseDto>.FailResponse(ex.Message));
            }
        }

        // PUT: api/contracts/5/fees/10
        [HttpPut("{id}/fees/{feeId}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> UpdateFee(int id, int feeId, [FromBody] UpdateContractFeeDto dto)
        {
            try
            {
                var fee = await _contractService.UpdateFeeAsync(id, feeId, dto);
                return Ok(ApiResponseDto<ContractFeeResponseDto>.SuccessResponse(fee, "تم تعديل الرسم بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<ContractFeeResponseDto>.FailResponse(ex.Message));
            }
        }

        // DELETE: api/contracts/5/fees/10
        [HttpDelete("{id}/fees/{feeId}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> DeleteFee(int id, int feeId)
        {
            var result = await _contractService.DeleteFeeAsync(id, feeId);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الرسم غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الرسم بنجاح"));
        }

        // POST: api/contracts/5/process-due
        [HttpPost("{id}/process-due")]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> ProcessDue(int id)
        {
            try
            {
                var payment = await _contractService.ProcessDueAsync(id);
                return Ok(ApiResponseDto<DTOs.Payments.PaymentResponseDto>.SuccessResponse(payment!, "تم خصم إيجار الشهر من رصيد المستأجر بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<string>.FailResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<string>.FailResponse(ex.Message));
            }
        }
    }
}