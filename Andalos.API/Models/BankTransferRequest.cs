using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class BankTransferRequest : BaseEntity
    {
        [Required]
        public int TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        // المبلغ الذي أدخله المستأجر
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RequestedAmount { get; set; }

        // المبلغ الفعلي بعد مراجعة وتعديل الإدارة (إن وُجد خطأ)
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ApprovedAmount { get; set; }

        public DateTime TransferDate { get; set; }

        [MaxLength(100)]
        public string? BankName { get; set; } // اسم البنك (اختياري)

        [MaxLength(100)]
        public string? ReferenceNumber { get; set; } // رقم الحوالة/الإيصال (اختياري)

        [Required]
        [MaxLength(500)]
        public string ReceiptFilePath { get; set; } = string.Empty; // مسار صورة الواصل

        public TransferRequestStatus Status { get; set; } = TransferRequestStatus.Pending;

        [MaxLength(500)]
        public string? AdminNotes { get; set; } // سبب الرفض أو ملاحظات الإدارة
    }
}