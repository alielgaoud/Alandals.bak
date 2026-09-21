using Andalos.API.Common;
using System.ComponentModel.DataAnnotations;

namespace Andalos.API.Models
{
    public class PermissionPackage : BaseEntity
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty; // مثل: الصيانة

        [MaxLength(300)]
        public string? Description { get; set; }

        public ICollection<PermissionPackageItem> Items { get; set; } = new List<PermissionPackageItem>();
        public ICollection<UserPermissionPackage> Users { get; set; } = new List<UserPermissionPackage>();
    }
}