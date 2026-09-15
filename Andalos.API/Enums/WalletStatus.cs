namespace Andalos.API.Enums
{
    public enum WalletStatus
    {
        Active = 1,     // رصيد متاح
        Depleted = 2,   // استُنفذ بالكامل (رصيد صفر)
        Expired = 3     // انتهت صلاحيته بنهاية اليوم
    }

    public enum SettlementMethod
    {
        Cash = 1,            // دُفعت للمحل نقداً
        BankTransfer = 2,    // حُولت لحساب المحل
        RentDeduction = 3    // خُصمت كجزء من إيجار المحل
    }
}