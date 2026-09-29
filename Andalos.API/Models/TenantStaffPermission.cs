using System.ComponentModel.DataAnnotations;
namespace Andalos.API.Models;
// Portal capabilities are intentionally NOT employee Module.Action permissions.
public class TenantStaffPermission
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(30)] public string Capability { get; set; } = "";
}
