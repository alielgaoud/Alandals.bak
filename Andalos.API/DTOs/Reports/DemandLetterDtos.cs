using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Reports
{
    public class GenerateDemandLetterDto
    {
        [Required]
        public int TenantId { get; set; }

        public int? ContractId { get; set; }

        /// <summary>
        /// تاريخ الاستحقاق الظاهر في الخطاب
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// من تاريخ (لحساب المستحقات)
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// إلى تاريخ (لحساب المستحقات)
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// وصف الفترة (مثال: الستة أشهر الأولى من السنة الثانية)
        /// إذا ترك فارغاً يتم توليده تلقائياً
        /// </summary>
        public string? PeriodDescription { get; set; }

        /// <summary>
        /// هل يشمل الإيجارات
        /// </summary>
        public bool IncludeRent { get; set; } = true;

        /// <summary>
        /// هل يشمل المصروفات المحملة
        /// </summary>
        public bool IncludeChargedExpenses { get; set; } = true;

        /// <summary>
        /// هل يشمل رسوم العقود (مرة واحدة/شهرية)
        /// </summary>
        public bool IncludeContractFees { get; set; } = true;

        /// <summary>
        /// نص إضافي اختياري في الخطاب
        /// </summary>
        public string? ExtraNote { get; set; }
    }

    public class DemandLetterItemDto
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public decimal Amount { get; set; }
        public string? Reference { get; set; }
    }

    public class DemandLetterDataDto
    {
        public string TenantName { get; set; } = string.Empty;
        public string? TenantPhone { get; set; }
        public string? NationalId { get; set; }

        public string? ContractNumber { get; set; }
        public string? UnitNumber { get; set; }
        public string? TradeName { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }

        public DateTime LetterDate { get; set; } = DateTime.Today;
        public DateTime DueDate { get; set; }
        public string PeriodDescription { get; set; } = string.Empty;

        public List<DemandLetterItemDto> Items { get; set; } = new();

        public decimal TotalAmount { get; set; }
        public string TotalAmountInWords { get; set; } = string.Empty;

        public string? ExtraNote { get; set; }
    } 

    // 👈 الـ DTO الجديد الخاص بتوليد المطالبة وإرسالها للمستأجر عبر النظام كإشعار
    public class SendDemandLetterDto : GenerateDemandLetterDto
    {
        public bool SendNotification { get; set; } = true;
        public string? NotificationTitle { get; set; }
        public string? NotificationMessage { get; set; }
    }
}