using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Andalos.API.Models
{
    public class Tenant : BaseEntity
    {
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string NationalId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContactPerson { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // 👈 جديد: رصيد المستأجر الدائن (المحفظة)
        [Column(TypeName = "decimal(18,2)")]
        public decimal CreditBalance { get; set; } = 0;
    }
}