using Andalos.API.DTOs.Common;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Andalos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _tenantService;

        public TenantsController(ITenantService tenantService)
        {
            _tenantService = tenantService;
        }

        // GET: api/tenants
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var tenants = await _tenantService.GetAllAsync();
            return Ok(ApiResponseDto<List<TenantResponseDto>>.SuccessResponse(tenants));
        }

        // GET: api/tenants/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var tenant = await _tenantService.GetByIdAsync(id);
            if (tenant == null)
                return NotFound(ApiResponseDto<TenantResponseDto>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<TenantResponseDto>.SuccessResponse(tenant));
        }

        // POST: api/tenants
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateTenantDto dto)
        {
            try
            {
                var tenant = await _tenantService.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = tenant.Id },
                    ApiResponseDto<TenantResponseDto>.SuccessResponse(tenant, "تم إضافة المستأجر بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<TenantResponseDto>.FailResponse(ex.Message));
            }
        }

        // PUT: api/tenants/5
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin,Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTenantDto dto)
        {
            try
            {
                var tenant = await _tenantService.UpdateAsync(id, dto);
                if (tenant == null)
                    return NotFound(ApiResponseDto<TenantResponseDto>.FailResponse("المستأجر غير موجود"));

                return Ok(ApiResponseDto<TenantResponseDto>.SuccessResponse(tenant, "تم تحديث بيانات المستأجر بنجاح"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponseDto<TenantResponseDto>.FailResponse(ex.Message));
            }
        }

        // DELETE: api/tenants/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _tenantService.DeleteAsync(id);
            if (!result)
                return NotFound(ApiResponseDto<bool>.FailResponse("المستأجر غير موجود"));

            return Ok(ApiResponseDto<bool>.SuccessResponse(true, "تم حذف المستأجر بنجاح"));
        }
    }
}