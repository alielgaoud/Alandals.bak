using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class PermissionPackageItem : BaseEntity
    {
        [Required]
        public int PackageId { get; set; }
        public PermissionPackage? Package { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionKey { get; set; } = string.Empty; // مثل: Maintenance.View
    }
}