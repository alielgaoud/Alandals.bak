using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Payments;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        // GET: api/payments
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var payments = await _paymentService.GetAllAsync();
            return Ok(ApiResponseDto<List<PaymentResponseDto>>.SuccessResponse(payments));
        }

        // GET: api/payments/contract/5
        [HttpGet("contract/{contractId}")]
        public async Task<IActionResult> GetByContract(int contractId)
        {
            var payments = await _paymentService.GetByContractAsync(contractId);
            return Ok(ApiResponseDto<List<PaymentResponseDto>>.SuccessResponse(payments));
        }

        // GET: api/payments/tenant/5
        [HttpGet("tenant/{tenantId}")]
        public async Task<IActionResult> GetByTenant(int tenantId)
        {
            var payments = await _paymentService.GetByTenantAsync(tenantId);
            return Ok(ApiResponseDto<List<PaymentResponseDto>>.SuccessResponse(payments));
        }

        // POST: api/payments
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin,Accountant")]
        public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
        {
            try
            {
                var payment = await _paymentService.CreateAsync(dto);
                return Ok(ApiResponseDto<PaymentResponseDto>.SuccessResponse(payment, "تم تسجيل الدفعة بنجاح"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<PaymentResponseDto>.FailResponse(ex.Message));
            }
        }

        // DELETE: api/payments/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _paymentService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("الدفعة غير موجودة"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف الدفعة بنجاح"));
        }

        // GET: api/payments/summary/5  (ملخص عقد واحد)
        [HttpGet("summary/{contractId}")]
        public async Task<IActionResult> GetContractSummary(int contractId)
        {
            try
            {
                var summary = await _paymentService.GetContractSummaryAsync(contractId);
                return Ok(ApiResponseDto<PaymentSummaryDto>.SuccessResponse(summary));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponseDto<PaymentSummaryDto>.FailResponse(ex.Message));
            }
        }

        // GET: api/payments/summary  (ملخص كل العقود)
        [HttpGet("summary")]
        public async Task<IActionResult> GetAllSummaries()
        {
            var summaries = await _paymentService.GetAllSummariesAsync();
            return Ok(ApiResponseDto<List<PaymentSummaryDto>>.SuccessResponse(summaries));
        }
    }
}