using System.ComponentModel.DataAnnotations;
namespace Andalos.API.Models;
public class GateCashReceipt
{
    public int Id { get; set; }
    public int VisitorPassId { get; set; }
    public int? ShiftId { get; set; } // null only for imported historical receipts
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(20)] public string Kind { get; set; } = "Issue";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
