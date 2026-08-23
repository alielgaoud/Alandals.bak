using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class ContractItem : BaseEntity
    {
        [Required]
        public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        [Required]
        [MaxLength(150)]
        public string ItemName { get; set; } = string.Empty; // اسم البند (مثال: رسوم نظافة، كهرباء ثابتة)

        [Required]
        public decimal Amount { get; set; }

        [MaxLength(200)]
        public string? Notes { get; set; }
    }
}