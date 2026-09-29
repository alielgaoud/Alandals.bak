using Andalos.API.Security;
using Andalos.API.Data;
using Andalos.API.DTOs.Visitors;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Andalos.API.Services
{
    public class VisitorPassService : IVisitorPassService
    {
        private readonly AppDbContext _db;
        private readonly CurrentUser _current;
        private readonly INotificationService _notification; // 👈 حقن الإشعارات

        public VisitorPassService(AppDbContext db, INotificationService notification, CurrentUser current)
        {
            _db = db;
            _current = current;
            _notification = notification;
        }

        public async Task<VisitorPassResponseDto> CreatePassAsync(CreateVisitorPassDto dto, string createdBy)
        {
            if (dto.MaxEntries is < 1 or > 1000 || !Enum.IsDefined(dto.VisitorType)) throw new ArgumentException("Invalid pass limits/type.");
            if (await IsBlacklistedAsync(dto.VisitorPhone, dto.NationalId)) throw new ForbiddenOperationException();
            int? owner = _current.Snapshot?.IsTenant == true ? _current.TenantId : null;
            if (owner is null && dto.UnitId.HasValue)
                owner = await _db.Contracts.Where(c => c.UnitId == dto.UnitId && c.IsActive && c.Status == ContractStatus.Active)
                    .Select(c => (int?)c.TenantId).FirstOrDefaultAsync();
            if (dto.UnitId.HasValue)
            {
                var unitExists = await _db.Units.AnyAsync(u => u.Id == dto.UnitId.Value && u.IsActive);
                if (!unitExists)
                    throw new KeyNotFoundException("المحل المحدد غير موجود");
            }

            // 🛡️ حماية تصادم الكود تحت الضغط: لو ولّد مستخدمان متزامنان نفس الكود العشوائي
            // نلتقط انتهاك الفهرس الفريد ونعيد التوليد والمحاولة (حتى 5 محاولات)
            for (int attempt = 1; ; attempt++)
            {
                string passCode = await GenerateUniquePassCodeAsync();

                var pass = new VisitorPass
                {
                    PassCode = passCode,
                    VisitorName = dto.VisitorName,
                    VisitorPhone = dto.VisitorPhone,
                    NationalId = dto.NationalId,
                    VisitorType = dto.VisitorType,
                    UnitId = dto.UnitId,
                    OwnerTenantId = owner,
                    ValidDate = dto.ValidDate.Date,
                    MaxEntries = dto.MaxEntries > 0 ? dto.MaxEntries : 1,
                    UsedCount = 0,
                    Status = PassStatus.Active,
                    Purpose = dto.Purpose,
                    Notes = dto.Notes,
                    CreatedBy = createdBy
                };

                try
                {
                    _db.VisitorPasses.Add(pass);
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < 5)
                {
                    _db.Entry(pass).State = EntityState.Detached;
                    continue;
                }

                var saved = await _db.VisitorPasses
                    .Include(p => p.Unit)
                    .FirstAsync(p => p.Id == pass.Id);

                return MapToDto(saved);
            }
        }

        // 👈 كشف انتهاك الفهرس الفريد في SQL Server (أخطاء 2601 / 2627)
        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
                && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
        }

        public async Task<VisitorPassResponseDto?> GetByIdAsync(int id)
        {
            var pass = await _db.VisitorPasses
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            return pass == null ? null : MapToDto(pass);
        }

        public async Task<VisitorPassResponseDto?> GetByCodeAsync(string passCode)
        {
            var pass = await _db.VisitorPasses
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.PassCode == passCode && p.IsActive);

            return pass == null ? null : MapToDto(pass);
        }

        public async Task<List<VisitorPassResponseDto>> GetAllAsync(DateTime? date, int? unitId)
        {
            var query = _db.VisitorPasses
                .Include(p => p.Unit)
                .Where(p => p.IsActive);

            if (date.HasValue)
                query = query.Where(p => p.ValidDate == date.Value.Date);

            if (unitId.HasValue)
                query = query.Where(p => p.UnitId == unitId.Value);

            // 🛡️ حد أقصى لحماية الذاكرة تحت الضغط + إسقاط SQL خالص (نفس إصلاح GetPagedAsync)
            var rows = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new
                {
                    p.Id,
                    p.PassCode,
                    p.VisitorName,
                    p.VisitorPhone,
                    p.NationalId,
                    p.VisitorType,
                    p.UnitId,
                    UnitNumber = p.Unit != null ? p.Unit.UnitNumber : null,
                    p.ValidDate,
                    p.MaxEntries,
                    p.UsedCount,
                    p.Status,
                    p.Purpose,
                    p.Notes,
                    p.CreatedAt
                })
                .ToListAsync();

            return rows.Select(r => new VisitorPassResponseDto
            {
                Id = r.Id,
                PassCode = r.PassCode,
                VisitorName = r.VisitorName,
                VisitorPhone = r.VisitorPhone,
                NationalId = r.NationalId,
                VisitorType = r.VisitorType.ToString(),
                UnitId = r.UnitId,
                UnitNumber = r.UnitNumber,
                UnitName = r.UnitNumber,
                ValidDate = r.ValidDate,
                MaxEntries = r.MaxEntries,
                UsedCount = r.UsedCount,
                Status = r.Status.ToString(),
                Purpose = r.Purpose,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        // 🛡️ قائمة مرقّمة صفحاتاً — للشاشات ذات الحجم الكبير (page/pageSize مع إجماليات)
        public async Task<object> GetPagedAsync(DateTime? date, int? unitId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _db.VisitorPasses
                .Where(p => p.IsActive);

            if (date.HasValue)
                query = query.Where(p => p.ValidDate == date.Value.Date);

            if (unitId.HasValue)
                query = query.Where(p => p.UnitId == unitId.Value);

            int totalCount = await query.CountAsync();

            // 🛡️ المرحلة 1: معرفات الصفحة فقط — شكل مُثبت السرعة (كان 9ms في كل القياسات)
            var ids = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => p.Id)
                .ToListAsync();

            // 🛡️ المرحلة 2: الصفوف عبر IN بإسقاط SQL مسطّح (يتجاوز الخطة المرضية للصيغة المتشعبة العريضة)
            var rows = await query
                .Where(p => ids.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.PassCode,
                    p.VisitorName,
                    p.VisitorPhone,
                    p.NationalId,
                    p.VisitorType,
                    p.UnitId,
                    UnitNumber = p.Unit != null ? p.Unit.UnitNumber : null,
                    p.ValidDate,
                    p.MaxEntries,
                    p.UsedCount,
                    p.Status,
                    p.Purpose,
                    p.Notes,
                    p.CreatedAt
                })
                .ToListAsync();

            // IN لا يضمن الترتيب — نعيد الترتيب في الذاكرة على 20 صفاً
            var order = new Dictionary<int, int>();
            for (int i = 0; i < ids.Count; i++) order[ids[i]] = i;

            var items = rows
                .OrderBy(r => order[r.Id])
                .Select(r => new VisitorPassResponseDto
                {
                    Id = r.Id,
                    PassCode = r.PassCode,
                    VisitorName = r.VisitorName,
                    VisitorPhone = r.VisitorPhone,
                    NationalId = r.NationalId,
                    VisitorType = r.VisitorType.ToString(),
                    UnitId = r.UnitId,
                    UnitNumber = r.UnitNumber,
                    UnitName = r.UnitNumber,
                    ValidDate = r.ValidDate,
                    MaxEntries = r.MaxEntries,
                    UsedCount = r.UsedCount,
                    Status = r.Status.ToString(),
                    Purpose = r.Purpose,
                    Notes = r.Notes,
                    CreatedAt = r.CreatedAt
                })
                .ToList();

            return new
            {
                items,
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }

        // 🛡️ حماية سباق المسح المتزامن (سكان بوابتين في نفس اللحظة):
        // معاملة Serializable تضمن أن ثاني حارس يقرأ UsedCount بعد تحديث الأول —
        // مستحيل رياضياً تجاوز MaxEntries مهما كان عدد المحاولات المتزامنة
        public async Task<ScanResultDto> ScanAndValidatePassAsync(ScanPassDto dto, string scannedBy)
        {
            return await _db.AtomicAsync(() => ScanCoreAsync(dto, scannedBy));
        }

        private Task<bool> IsBlacklistedAsync(string? phone, string? nationalId) => _db.VisitorBlacklists.AnyAsync(b =>
            b.IsActive && (b.IsPermanent || b.ExpiresAt == null || b.ExpiresAt > DateTimeHelper.LibyaNow) &&
            ((phone != null && phone != "" && b.Phone == phone) || (nationalId != null && nationalId != "" && b.NationalId == nationalId)));

        private async Task<ScanResultDto> ScanCoreAsync(ScanPassDto dto, string scannedBy)
        {
            var pass = await _db.VisitorPasses
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.PassCode == dto.PassCode && p.IsActive);

            var today = DateTimeHelper.LibyaToday;

            if (pass == null)
            {
                return new ScanResultDto
                {
                    IsSuccess = false,
                    Message = "❌ رمز التصريح غير صحيح أو غير موجود بالنظام"
                };
            }

            if (await IsBlacklistedAsync(pass.VisitorPhone, pass.NationalId))
            {
                await LogEntryAsync(pass.Id, dto.GateName, scannedBy, false, "Security restriction");
                return FailResult(pass, "", "غير مسموح بالدخول.");
            }

            string destination = pass.Unit != null
                 ? $"محل رقم: {pass.Unit.UnitNumber}"
                 : "إدارة المجمع";

            if (pass.Status == PassStatus.Revoked)
            {
                await LogEntryAsync(pass.Id, dto.GateName, scannedBy, false, "التصريح ملغي من قبل الإدارة أو المحل");
                return FailResult(pass, destination, "❌ هذا التصريح ملغي ولا يسمح بالدخول به");
            }

            if (pass.Status == PassStatus.Used)
            {
                await LogEntryAsync(pass.Id, dto.GateName, scannedBy, false, "تم استنفاد مرات الدخول المسموحة مسبقاً");
                return FailResult(pass, destination, "❌ تم استخدام هذا التصريح واستنفاد عدد مرات الدخول");
            }

            if (pass.ValidDate.Date != today)
            {
                string reason = pass.ValidDate.Date < today
                    ? "التصريح منتهي الصلاحية (تاريخ سابق)"
                    : $"التصريح صالح ليوم {pass.ValidDate:yyyy-MM-dd} وليس لليوم";

                pass.Status = pass.ValidDate.Date < today ? PassStatus.Expired : pass.Status;
                await _db.SaveChangesAsync();

                await LogEntryAsync(pass.Id, dto.GateName, scannedBy, false, reason);
                return FailResult(pass, destination, $"❌ غير مسموح بالدخول: {reason}");
            }

            if (pass.UsedCount >= pass.MaxEntries)
            {
                pass.Status = PassStatus.Used;
                await _db.SaveChangesAsync();

                await LogEntryAsync(pass.Id, dto.GateName, scannedBy, false, "استنفاد جميع مرات الدخول");
                return FailResult(pass, destination, "❌ تم استنفاد الحد الأقصى للدخول بهذا التصريح");
            }

            pass.UsedCount++;
            if (pass.UsedCount >= pass.MaxEntries)
            {
                pass.Status = PassStatus.Used;
            }
            pass.UpdatedAt = DateTimeHelper.LibyaNow;

            await _db.SaveChangesAsync();
            await LogEntryAsync(pass.Id, dto.GateName, scannedBy, true, null);

            // 💡 [سحر الربط]: رصد المستأجر الحالي للمحل لإرسال إشعار لحظي له يفيد بدخول زائره الآن 💡
            if (pass.OwnerTenantId.HasValue)
                await _notification.SendToTenantAsync(pass.OwnerTenantId.Value, "وصول زائر للمحل", $"تم تسجيل دخول زائركم من بوابة {dto.GateName}.",
                    NotificationType.VisitorEntered, "/tenant/visitors", pass.Id);

            return new ScanResultDto
            {
                IsSuccess = true,
                Message = "✅ تصريح سليم - مسموح بالدخول",
                VisitorName = pass.VisitorName,
                VisitorPhone = pass.VisitorPhone,
                VisitorType = pass.VisitorType.ToString(),
                DestinationUnit = destination,
                Purpose = pass.Purpose,
                ScanTime = DateTime.Now,
                RemainingEntries = pass.MaxEntries - pass.UsedCount
            };
        }

        public async Task<bool> RevokePassAsync(int id)
        {
            var pass = await _db.VisitorPasses.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (pass == null) return false;

            pass.Status = PassStatus.Revoked;
            pass.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<List<EntryLogResponseDto>> GetEntryLogsAsync(DateTime? date)
        {
            var query = _db.EntryLogs
                .Include(e => e.VisitorPass)
                    .ThenInclude(p => p!.Unit)
                .Where(e => e.IsActive);

            if (date.HasValue)
                query = query.Where(e => e.ScanTime.Date == date.Value.Date);

            return await query
                .OrderByDescending(e => e.ScanTime)
                .Select(e => new EntryLogResponseDto
                {
                    Id = e.Id,
                    PassCode = e.VisitorPass != null ? e.VisitorPass.PassCode : "",
                    VisitorName = e.VisitorPass != null ? e.VisitorPass.VisitorName : "",
                    DestinationUnit = e.VisitorPass != null && e.VisitorPass.Unit != null
                        ? $"محل رقم {e.VisitorPass.Unit.UnitNumber}"
                        : "الإدارة",
                    ScanTime = e.ScanTime,
                    GateName = e.GateName,
                    ScannedBy = e.ScannedBy,
                    IsAllowed = e.IsAllowed,
                    RejectReason = e.RejectReason
                })
                .ToListAsync();
        }

        private async Task LogEntryAsync(int passId, string gateName, string scannedBy, bool isAllowed, string? reason)
        {
            var log = new EntryLog
            {
                VisitorPassId = passId,
                GateName = gateName,
                ScannedBy = scannedBy,
                ScanTime = DateTimeHelper.LibyaNow,
                IsAllowed = isAllowed,
                RejectReason = reason
            };
            _db.EntryLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        private static ScanResultDto FailResult(VisitorPass pass, string destination, string message)
        {
            return new ScanResultDto
            {
                IsSuccess = false,
                Message = message,
                VisitorName = pass.VisitorName,
                VisitorPhone = pass.VisitorPhone,
                VisitorType = pass.VisitorType.ToString(),
                DestinationUnit = destination,
                Purpose = pass.Purpose,
                ScanTime = DateTimeHelper.LibyaNow,
                RemainingEntries = Math.Max(0, pass.MaxEntries - pass.UsedCount)
            };
        }

        private async Task<string> GenerateUniquePassCodeAsync()
        {
            string passCode;
            bool exists;
            string datePrefix = DateTimeHelper.LibyaNow.ToString("yyyyMMdd");

            do
            {
                string randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
                passCode = $"PASS-{datePrefix}-{randomHex}";
                exists = await _db.VisitorPasses.AnyAsync(p => p.PassCode == passCode);
            }
            while (exists);

            return passCode;
        }

        private static VisitorPassResponseDto MapToDto(VisitorPass p)
        {
            return new VisitorPassResponseDto
            {
                Id = p.Id,
                PassCode = p.PassCode,
                VisitorName = p.VisitorName,
                VisitorPhone = p.VisitorPhone,
                NationalId = p.NationalId,
                VisitorType = p.VisitorType.ToString(),
                UnitId = p.UnitId,
                UnitNumber = p.Unit?.UnitNumber,
                UnitName = p.Unit?.UnitNumber,
                ValidDate = p.ValidDate,
                MaxEntries = p.MaxEntries,
                UsedCount = p.UsedCount,
                Status = p.Status.ToString(),
                Purpose = p.Purpose,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt
            };
        }
    }
}