using Andalos.API.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Tenants
{
    // 1. المستأجر يرفع الطلب
    public class SubmitTransferRequestDto
    {
        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Range(0.01, 1000000)]
        public decimal RequestedAmount { get; set; }

        public DateTime TransferDate { get; set; } = DateTime.Now;

        public string? BankName { get; set; }
        public string? ReferenceNumber { get; set; }

        [Required(ErrorMessage = "صورة واصل الدفع مطلوبة")]
        public IFormFile ReceiptFile { get; set; } = null!;
    }

    // 2. الإدارة تراجع وتقبل/ترفض
    public class ReviewTransferRequestDto
    {
        [Required]
        public bool IsApproved { get; set; }

        // الإدارة يمكنها تعديل المبلغ ليكون الصحيح قبل الموافقة
        [Range(0.01, 1000000)]
        public decimal? CorrectedAmount { get; set; }

        public string? AdminNotes { get; set; }
    }

    // 3. عرض الطلب
    public class TransferRequestResponseDto
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public decimal RequestedAmount { get; set; }
        public decimal? ApprovedAmount { get; set; }
        public DateTime TransferDate { get; set; }
        public string? BankName { get; set; }
        public string? ReferenceNumber { get; set; }
        public string ReceiptFilePath { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}