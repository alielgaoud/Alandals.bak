namespace Andalos.API.Services
{
    public sealed class PermissionVersionConflictException : Exception
    {
        public PermissionVersionConflictException()
            : base("تغير إصدار صلاحيات المستخدم؛ أعد تحميلها قبل الحفظ")
        {
        }

        public PermissionVersionConflictException(Exception inner)
            : base("تغير إصدار صلاحيات المستخدم؛ أعد تحميلها قبل الحفظ", inner)
        {
        }
    }
}