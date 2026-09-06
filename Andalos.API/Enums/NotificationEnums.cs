namespace Andalos.API.Enums
{
    // نوع الإشعار
    public enum NotificationType
    {
        // إشعارات عامة
        General = 1,
        System = 2,

        // إشعارات الشكاوى
        NewComplaint = 10,          // للإدارة: شكوى جديدة
        ComplaintReply = 11,        // للمستأجر: رد على شكواك
        ComplaintStatusChanged = 12,

        // إشعارات الحوالات
        NewBankTransfer = 20,       // للإدارة: حوالة جديدة تحتاج مراجعة
        BankTransferApproved = 21,  // للمستأجر: تم قبول حوالتك
        BankTransferRejected = 22,  // للمستأجر: تم رفض حوالتك

        // إشعارات العقود
        ContractExpiringSoon = 30,      // عقد ينتهي قريباً
        ContractRenewed = 31,           // تم تجديد العقد
        ContractTerminated = 32,

        // إشعارات المدفوعات
        PaymentReminder = 40,           // للمستأجر: تذكير باستحقاق
        PaymentOverdue = 41,            // للإدارة: متأخرات على مستأجر
        AutomaticDeduction = 42,        // للمستأجر: تم خصم من رصيدك
        PaymentReceived = 43,           // للإدارة: تم استلام دفعة

        // إشعارات الصيانة
        NewMaintenanceRequest = 50,     // للإدارة: طلب صيانة جديد
        MaintenanceStatusChanged = 51,  // للمستأجر: تحديث طلب الصيانة

        // إشعارات الزوار
        VisitorRejected = 60,           // للإدارة: محاولة دخول مرفوضة
        VisitorEntered = 61
    }

    // أولوية الإشعار
    public enum NotificationPriority
    {
        Low = 1,        // منخفضة (معلوماتي)
        Medium = 2,     // متوسطة (طبيعي)
        High = 3,       // عالية (يحتاج انتباه)
        Urgent = 4      // عاجل (يحتاج تصرف فوري)
    }

    // قناة الإرسال
    public enum NotificationChannel
    {
        InApp = 1,      // داخل التطبيق
        Push = 2,       // إشعار جوال Push
        Email = 3,      // بريد إلكتروني (للمستقبل)
        SMS = 4         // رسائل نصية (للمستقبل)
    }
}