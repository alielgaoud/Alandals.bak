using System.ComponentModel.DataAnnotations;
namespace Andalos.API.Models;
public class ProtectedDocument
{
    public int Id { get; set; }
    [MaxLength(500)] public string Path { get; set; } = "";
    public int TenantId { get; set; }
    [MaxLength(100)] public string PermissionKey { get; set; } = "Demands.GeneratePdf";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
