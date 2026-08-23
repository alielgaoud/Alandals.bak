namespace Andalos.API.Enums
{
    public enum UnitStatus
    {
        Vacant = 1,        // شاغر
        Rented = 2,        // مؤجر
        Maintenance = 3,   // صيانة
        Reserved = 4       // محجوز
    }
    public enum UnitType
    {
        Shop = 1,          // محل تجاري
        Cafe = 2,          // كافيه
        Restaurant = 3,    // مطعم
        Office = 4,        // مكتب
        Warehouse = 5,     // مخزن
        Kiosk = 6,         // كشك
        Hall = 7,          // قاعة
        Other = 8          // أخرى
    }
}