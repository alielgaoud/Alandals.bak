namespace Andalos.API.Enums
{
    public enum TransferRequestStatus
    {
        Pending = 1,  // قيد المراجعة
        Approved = 2, // تمت الموافقة (تم إيداع المبلغ)
        Rejected = 3  // مرفوض
    }
}