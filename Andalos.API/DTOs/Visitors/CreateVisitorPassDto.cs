using Andalos.API.Enums;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.DTOs.Visitors
{
    public class CreateVisitorPassDto
    {
        [Required(ErrorMessage = "اسم الزائر مطلوب")]
        [MaxLength(150)]
        public string VisitorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم هاتف الزائر مطلوب")]
        [MaxLength(20)]
        public string VisitorPhone { get; set; } = string.Empty;

        public string? NationalId { get; set; }

        public VisitorType VisitorType { get; set; } = VisitorType.Customer;

        public int? UnitId { get; set; } // null إذا كان الزائر للإدارة

        [Required(ErrorMessage = "تاريخ الزيارة مطلوب")]
        public DateTime ValidDate { get; set; } = DateTime.Today;

        public int MaxEntries { get; set; } = 1;

        public string? Purpose { get; set; }

        public string? Notes { get; set; }
    }
    public class VisitorPassResponseDto
    {
        public int Id { get; set; }
        public string PassCode { get; set; } = string.Empty;
        public string VisitorName { get; set; } = string.Empty;
        public string VisitorPhone { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string VisitorType { get; set; } = string.Empty;
        public int? UnitId { get; set; }
        public string? UnitNumber { get; set; }
        public string? UnitName { get; set; }
        public DateTime ValidDate { get; set; }
        public int MaxEntries { get; set; }
        public int UsedCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Purpose { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class ScanPassDto
    {
        [Required(ErrorMessage = "رمز الباركود مطلوب")]
        public string PassCode { get; set; } = string.Empty;

        public string GateName { get; set; } = "البوابة الرئيسية";
    }

    public class ScanResultDto
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? VisitorName { get; set; }
        public string? VisitorPhone { get; set; }
        public string? VisitorType { get; set; }
        public string? DestinationUnit { get; set; } // اسم المحل المقصود أو الإدارة
        public string? Purpose { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.Now;
        public int RemainingEntries { get; set; }
    }

    public class EntryLogResponseDto
    {
        public int Id { get; set; }
        public string PassCode { get; set; } = string.Empty;
        public string VisitorName { get; set; } = string.Empty;
        public string? DestinationUnit { get; set; }
        public DateTime ScanTime { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? ScannedBy { get; set; }
        public bool IsAllowed { get; set; }
        public string? RejectReason { get; set; }
    }
}