using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class EntryLog : BaseEntity
    {
        [Required]
        public int VisitorPassId { get; set; }
        public VisitorPass? VisitorPass { get; set; }

        public DateTime ScanTime { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string GateName { get; set; } = "البوابة الرئيسية";

        [MaxLength(100)]
        public string? ScannedBy { get; set; } // اسم أو معرف حارس البوابة

        public bool IsAllowed { get; set; } // هل تم السماح بالدخول أم تم الرفض؟

        [MaxLength(255)]
        public string? RejectReason { get; set; } // سبب الرفض في حال كان غير مسموح
    }
}