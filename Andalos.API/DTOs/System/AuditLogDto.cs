namespace Andalos.API.DTOs.System
{
    public class AuditLogDto
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string AuditType { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string PrimaryKey { get; set; } = string.Empty;
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? AffectedColumns { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}