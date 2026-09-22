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
        private readonly INotificationService _notification; // 👈 حقن الإشعارات

        public VisitorPassService(AppDbContext db, INotificationService notification)
        {
            _db = db;
            _notification = notification;
        }

        public async Task<VisitorPassResponseDto> CreatePassAsync(CreateVisitorPassDto dto, string createdBy)
        {
            if (dto.UnitId.HasValue)
            {
                var unitExists = await _db.Units.AnyAsync(u => u.Id == dto.UnitId.Value && u.IsActive);
                if (!unitExists)
                    throw new KeyNotFoundException("المحل المحدد غير موجود");
            }

            string passCode = await GenerateUniquePassCodeAsync();

            var pass = new VisitorPass
            {
                PassCode = passCode,
                VisitorName = dto.VisitorName,
                VisitorPhone = dto.VisitorPhone,
                NationalId = dto.NationalId,
                VisitorType = dto.VisitorType,
                UnitId = dto.UnitId,
                ValidDate = dto.ValidDate.Date,
                MaxEntries = dto.MaxEntries > 0 ? dto.MaxEntries : 1,
                UsedCount = 0,
                Status = PassStatus.Active,
                Purpose = dto.Purpose,
                Notes = dto.Notes,
                CreatedBy = createdBy
            };

            _db.VisitorPasses.Add(pass);
            await _db.SaveChangesAsync();

            var saved = await _db.VisitorPasses
                .Include(p => p.Unit)
                .FirstAsync(p => p.Id == pass.Id);

            return MapToDto(saved);
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

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => MapToDto(p))
                .ToListAsync();
        }

        public async Task<ScanResultDto> ScanAndValidatePassAsync(ScanPassDto dto, string scannedBy)
        {
            var pass = await _db.VisitorPasses
                .Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.PassCode == dto.PassCode && p.IsActive);

            var today = DateTime.Today;

            if (pass == null)
            {
                return new ScanResultDto
                {
                    IsSuccess = false,
                    Message = "❌ رمز التصريح غير صحيح أو غير موجود بالنظام"
                };
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
            if (pass.UnitId.HasValue)
            {
                // جلب العقد النشط الحالي للمحل للوصول للمستأجر
                var activeContract = await _db.Contracts
                    .FirstOrDefaultAsync(c => c.UnitId == pass.UnitId.Value && c.Status == ContractStatus.Active && c.IsActive);

                if (activeContract != null)
                {
                    _ = _notification.SendToTenantAsync(
                        activeContract.TenantId,
                        "وصول زائر للمحل 🚪",
                        $"نعلمكم بأن زائرك ({pass.VisitorName}) قد تم تسجيل دخوله الآن من بوابة: {dto.GateName}.",
                        NotificationType.VisitorEntered,
                        "/tenant/visitors",
                        pass.Id
                    );
                }
            }

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