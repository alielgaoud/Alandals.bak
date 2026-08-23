using Andalos.API.Common;
using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class VisitorPass : BaseEntity
    {
        [Required]
        [MaxLength(64)]
        public string PassCode { get; set; } = string.Empty; // كود الباركود الفريد المشفر

        [Required]
        [MaxLength(150)]
        public string VisitorName { get; set; } = string.Empty; // اسم الزائر

        [Required]
        [MaxLength(20)]
        public string VisitorPhone { get; set; } = string.Empty; // هاتف الزائر

        [MaxLength(50)]
        public string? NationalId { get; set; } // رقم الهوية إن وُجد

        public VisitorType VisitorType { get; set; } = VisitorType.Customer;

        public int? UnitId { get; set; } // المحل المرتبط بالزيارة (null إذا كانت الزيارة خاصة بالإدارة)
        public Unit? Unit { get; set; }

        [Required]
        public DateTime ValidDate { get; set; } = DateTime.Today; // يوم الصلاحية المسموح به للدخول فقط

        public int MaxEntries { get; set; } = 1; // أقصى عدد مرات دخول مسموحة (الافتراضي: 1)

        public int UsedCount { get; set; } = 0; // كم مرة دخل فعلياً

        public PassStatus Status { get; set; } = PassStatus.Active;

        [MaxLength(300)]
        public string? Purpose { get; set; } // الغرض من الزيارة

        [MaxLength(300)]
        public string? Notes { get; set; }

        public ICollection<EntryLog> EntryLogs { get; set; } = new List<EntryLog>();
    }
}