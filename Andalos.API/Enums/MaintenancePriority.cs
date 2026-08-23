namespace Andalos.API.Enums
{
    public enum MaintenancePriority
    {
        Low = 1,       // منخفضة
        Medium = 2,    // متوسطة
        High = 3,      // عاجلة
        Urgent = 4     // طارئة جداً
    }
    public enum MaintenanceStatus
    {
        New = 1,         // جديد
        InProgress = 2,  // قيد التنفيذ
        Completed = 3,   // مكتمل
        Cancelled = 4    // ملغي
    }
    public enum MaintenanceType
    {
        Electrical = 1,   // كهرباء
        Plumbing = 2,     // سباكة
        AirCondition = 3, // تكييف وتبريد
        Structural = 4,   // أعمال إنشائية/ديكور
        Cleaning = 5,     // نظافة
        Other = 6         // أخرى
    }
}