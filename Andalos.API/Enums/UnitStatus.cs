namespace Andalos.API.Enums
{
    public enum UnitStatus
    {
        Vacant = 1,        // شاغر
        Rented = 2,        // مؤجر
        Maintenance = 3,   // صيانة
        Reserved = 4       // محجوز
    }
    public enum ActivityType
    {
        Restaurant = 1,    // مطعم
        Cafe = 2,          // كافيه
        Clothing = 3,      // ملابس
        Pharmacy = 4,      // صيدلية
        Supermarket = 5,   // سوبر ماركت
        Electronics = 6,   // إلكترونيات
        Salon = 7,         // صالون تجميل
        Office = 8,        // مكتب
        Warehouse = 9,     // مخزن
        Kiosk = 10,        // كشك
        Hall = 11,         // قاعة أفراح/مناسبات
        Workshop = 12,     // ورشة
        Other = 13         // أخرى
    }
}