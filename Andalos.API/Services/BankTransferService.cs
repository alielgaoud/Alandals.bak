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
        private readonly ITenantAccountService _accountService;
        private readonly INotificationService _notification;

        public BankTransferService(AppDbContext db, ITenantAccountService accountService, INotificationService notification)
        {
            _db = db;
            _accountService = accountService;
            _notification = notification;
        }

        // ===== 1. المستأجر يرفع الطلب =====
        public async Task<TransferRequestResponseDto> SubmitRequestAsync(int tenantId, SubmitTransferRequestDto dto, string uploadsFolder)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive);
            if (tenant == null)
                throw new KeyNotFoundException("المستأجر غير موجود أو غير نشط");

            string receiptsDirectory = Path.Combine(uploadsFolder, "uploads", "receipts");
            if (!Directory.Exists(receiptsDirectory))
            {
                Directory.CreateDirectory(receiptsDirectory);
            }

            string fileExtension = Path.GetExtension(dto.ReceiptFile.FileName);
            if (string.IsNullOrEmpty(fileExtension)) fileExtension = ".jpg";

            string fileName = $"{Guid.NewGuid()}{fileExtension}";
            string fullPath = Path.Combine(receiptsDirectory, fileName);

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

            // 🔔 إشعار فوري للإدارة بوجود حوالة جديدة
            _ = _notification.SendToAllAdminsAsync(
                "حوالة بنكية جديدة بانتظار المراجعة 💳",
                $"المستأجر {tenant.FullName} رفع حوالة بنكية بقيمة {dto.RequestedAmount:N2} د.ل - بنك {dto.BankName} - مرجع {dto.ReferenceNumber}",
                NotificationType.NewBankTransfer,
                NotificationPriority.High,
                $"/admin/bank-transfers/{request.Id}",
                request.Id
            );

            // 🔔 تأكيد للمستأجر
            _ = _notification.SendToTenantAsync(
                tenantId,
                "تم استلام إيصال الحوالة بنجاح 📤",
                $"تم رفع إيصال الحوالة بقيمة {dto.RequestedAmount:N2} د.ل بنجاح، وسيتم مراجعته من الإدارة قريباً.",
                NotificationType.NewBankTransfer,
                $"/tenant/bank-transfers",
                request.Id
            );

            return MapToDto(request, tenant.FullName);
        }

        // ===== 2. الإدارة تستعرض الطلبات =====
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
                decimal finalAmount = dto.CorrectedAmount ?? request.RequestedAmount;
                request.ApprovedAmount = finalAmount;
                request.AdminNotes = dto.AdminNotes;

                string depositNotes = $"حوالة بنكية معتمدة (طلب رقم {request.Id}) {(request.BankName != null ? $"- بنك {request.BankName}" : "")}";

                await _accountService.DepositAdvancePaymentAsync(
                    request.TenantId,
                    finalAmount,
                    PaymentMethod.Transfer,
                    depositNotes
                );

                // 🔔 إشعار فوري للمستأجر بالقبول وإيداع الرصيد
                _ = _notification.SendToTenantAsync(
                    request.TenantId,
                    "تم قبول الحوالة البنكية وإيداع الرصيد ✅",
                    $"تمت الموافقة على حوالتك البنكية بقيمة {finalAmount:N2} د.ل وتم إيداعها في محفظتك. {(dto.CorrectedAmount.HasValue ? $"(تم تصحيح المبلغ من {request.RequestedAmount:N2})" : "")}",
                    NotificationType.BankTransferApproved,
                    $"/tenant/payments",
                    request.Id
                );
            }
            else
            {
                request.Status = TransferRequestStatus.Rejected;
                request.AdminNotes = dto.AdminNotes ?? "تم الرفض لعدم صحة البيانات المرفقة";

                // 🔔 إشعار فوري بالرفض
                _ = _notification.SendToTenantAsync(
                    request.TenantId,
                    "تم رفض الحوالة البنكية ❌",
                    $"نأسف، تم رفض الحوالة البنكية بقيمة {request.RequestedAmount:N2} د.ل. السبب: {request.AdminNotes}",
                    NotificationType.BankTransferRejected,
                    $"/tenant/bank-transfers",
                    request.Id
                );
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