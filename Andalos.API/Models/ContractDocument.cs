using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class ContractDocument : BaseEntity
    {
        [Required]
        public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? FileType { get; set; } // pdf, png, jpg
    }
}