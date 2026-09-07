using Andalos.API.Data;
using Andalos.API.DTOs.Tenants;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public interface IBankTransferService
    {
        Task<TransferRequestResponseDto> SubmitRequestAsync(int tenantId, SubmitTransferRequestDto dto, string uploadsFolder);
        Task<List<TransferRequestResponseDto>> GetRequestsAsync(TransferRequestStatus? status = null);
        Task<TransferRequestResponseDto> ReviewRequestAsync(int requestId, ReviewTransferRequestDto dto);
    }

    public class BankTransferService : IBankTransferService
    {
        private readonly AppDbContext _db;
        private readonly ITenantAccountService _accountService; // 👈 لاستدعاء المحفظة

        public BankTransferService(AppDbContext db, ITenantAccountService accountService)
        {
            _db = db;
            _accountService = accountService;
        }

        // ===== 1. المستأجر يرفع الطلب =====
        public async Task<TransferRequestResponseDto> SubmitRequestAsync(int tenantId, SubmitTransferRequestDto dto, string uploadsFolder)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("المستأجر غير موجود أو غير نشط");

            // 👈 إنشاء مسار wwwroot/uploads/receipts بالكامل بشكل أمن على القرص
            string receiptsDirectory = Path.Combine(uploadsFolder, "uploads", "receipts");
            if (!Directory.Exists(receiptsDirectory))
            {
                Directory.CreateDirectory(receiptsDirectory);
            }

            string fileExtension = Path.GetExtension(dto.ReceiptFile.FileName);
            if (string.IsNullOrEmpty(fileExtension)) fileExtension = ".jpg";

            string fileName = $"{Guid.NewGuid()}{fileExtension}";
            string fullPath = Path.Combine(receiptsDirectory, fileName);

            // كتابة الملف بأمان
            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await dto.ReceiptFile.CopyToAsync(stream);
            }

            var request = new BankTransferRequest
            {
                TenantId = tenantId,
                RequestedAmount = dto.RequestedAmount,
                TransferDate = dto.TransferDate,
                BankName = dto.BankName,
                ReferenceNumber = dto.ReferenceNumber,
                ReceiptFilePath = $"/uploads/receipts/{fileName}",
                Status = TransferRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.BankTransferRequests.Add(request);
            await _db.SaveChangesAsync();

            return MapToDto(request, tenant.FullName);
        }

        // ===== 2. الإدارة تستعرض الطلبات (يمكن الفلترة لمعرفة المعلق فقط) =====
        public async Task<List<TransferRequestResponseDto>> GetRequestsAsync(TransferRequestStatus? status = null)
        {
            var query = _db.BankTransferRequests.Include(r => r.Tenant).Where(r => r.IsActive);
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            var list = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
            return list.Select(r => MapToDto(r, r.Tenant?.FullName ?? "")).ToList();
        }

        // ===== 3. الإدارة توافق وتعدل المبلغ أو ترفض =====
        public async Task<TransferRequestResponseDto> ReviewRequestAsync(int requestId, ReviewTransferRequestDto dto)
        {
            var request = await _db.BankTransferRequests.Include(r => r.Tenant)
                                   .FirstOrDefaultAsync(r => r.Id == requestId && r.IsActive);

            if (request == null) throw new KeyNotFoundException("الطلب غير موجود");
            if (request.Status != TransferRequestStatus.Pending) throw new InvalidOperationException("تمت مراجعة هذا الطلب مسبقاً");

            if (dto.IsApproved)
            {
                request.Status = TransferRequestStatus.Approved;
                // إذا أدخلت الإدارة مبلغاً مصححاً، اعتمده، وإلا اعتمد مبلغ المستأجر
                decimal finalAmount = dto.CorrectedAmount ?? request.RequestedAmount;
                request.ApprovedAmount = finalAmount;
                request.AdminNotes = dto.AdminNotes;

                // 💡 سحر الربط: إيداع المبلغ تلقائياً في محفظة المستأجر 💡
                string depositNotes = $"حوالة بنكية معتمدة (طلب رقم {request.Id}) {(request.BankName != null ? $"- بنك {request.BankName}" : "")}";

                await _accountService.DepositAdvancePaymentAsync(
                    request.TenantId,
                    finalAmount,
                    PaymentMethod.Transfer,
                    depositNotes
                );
            }
            else
            {
                request.Status = TransferRequestStatus.Rejected;
                request.AdminNotes = dto.AdminNotes ?? "تم الرفض لعدم صحة البيانات المرفقة";
            }

            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return MapToDto(request, request.Tenant?.FullName ?? "");
        }

        private static TransferRequestResponseDto MapToDto(BankTransferRequest req, string tenantName)
        {
            return new TransferRequestResponseDto
            {
                Id = req.Id,
                TenantId = req.TenantId,
                TenantName = tenantName,
                RequestedAmount = req.RequestedAmount,
                ApprovedAmount = req.ApprovedAmount,
                TransferDate = req.TransferDate,
                BankName = req.BankName,
                ReferenceNumber = req.ReferenceNumber,
                ReceiptFilePath = req.ReceiptFilePath,
                Status = req.Status.ToString(),
                AdminNotes = req.AdminNotes,
                CreatedAt = req.CreatedAt
            };
        }
    }
}