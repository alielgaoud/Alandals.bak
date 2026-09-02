namespace Andalos.API.Enums
{
    public enum ComplaintStatus
    {
        New = 1,          // جديدة (لم تُقرأ بعد)
        InProgress = 2,   // قيد المعالجة
        Resolved = 3,     // تم الحل
        Closed = 4        // مغلقة
    }
}