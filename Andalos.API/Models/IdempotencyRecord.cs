using System.ComponentModel.DataAnnotations;
namespace Andalos.API.Models;
public class IdempotencyRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [MaxLength(180)] public string Operation { get; set; } = "";
    [MaxLength(64)] public string KeyHash { get; set; } = "";
    [MaxLength(64)] public string RequestHash { get; set; } = "";
    public int StatusCode { get; set; }
    public string ResponseJson { get; set; } = "";
    [MaxLength(1000)] public string? Location { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
