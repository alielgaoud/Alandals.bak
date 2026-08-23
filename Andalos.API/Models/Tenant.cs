using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class Tenant : BaseEntity
    {
        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string NationalId { get; set; } = string.Empty; // رقم الهوية أو جواز السفر

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ContactPerson { get; set; }  // الشخص المسؤول عن التواصل

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}