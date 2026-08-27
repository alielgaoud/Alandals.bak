using Andalos.API.Data;
using Andalos.API.DTOs.Blacklist;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class VisitorBlacklistService : IVisitorBlacklistService
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public VisitorBlacklistService(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        public async Task<List<BlacklistResponseDto>> GetAllAsync()
        {
            var now = DateTime.Now;
            return await _db.VisitorBlacklists
                .Where(b => b.IsActive)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new BlacklistResponseDto
                {
                    Id = b.Id,
                    FullName = b.FullName,
                    NationalId = b.NationalId,
                    Phone = b.Phone,
                    Reason = b.Reason,
                    IncidentDate = b.IncidentDate,
                    IsPermanent = b.IsPermanent,
                    ExpiresAt = b.ExpiresAt,
                    IsCurrentlyBlocked = b.IsPermanent || (b.ExpiresAt.HasValue && b.ExpiresAt.Value >= now),
                    Notes = b.Notes,
                    CreatedBy = b.CreatedBy,
                    CreatedAt = b.CreatedAt,
                    AttachmentUrl = b.AttachmentUrl
                })
                .ToListAsync();
        }

        public async Task<BlacklistResponseDto?> GetByIdAsync(int id)
        {
            var b = await _db.VisitorBlacklists
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

            if (b == null) return null;

            var now = DateTime.Now;
            return new BlacklistResponseDto
            {
                Id = b.Id,
                FullName = b.FullName,
                NationalId = b.NationalId,
                Phone = b.Phone,
                Reason = b.Reason,
                IncidentDate = b.IncidentDate,
                IsPermanent = b.IsPermanent,
                ExpiresAt = b.ExpiresAt,
                IsCurrentlyBlocked = b.IsPermanent || (b.ExpiresAt.HasValue && b.ExpiresAt.Value >= now),
                Notes = b.Notes,
                CreatedBy = b.CreatedBy,
                CreatedAt = b.CreatedAt,
                AttachmentUrl = b.AttachmentUrl
            };
        }

        public async Task<BlacklistResponseDto> AddAsync(CreateBlacklistDto dto, string createdBy)
        {
            var cleanNationalId = dto.NationalId?.Trim();
            var cleanPhone = dto.Phone?.Trim();

            // فحص تكرار الرقم الوطني إن وُجد
            if (!string.IsNullOrEmpty(cleanNationalId))
            {
                var nationalIdExists = await _db.VisitorBlacklists
                    .AnyAsync(b => b.NationalId == cleanNationalId && b.IsActive);

                if (nationalIdExists)
                    throw new InvalidOperationException("رقم الهوية هذا موجود مسبقاً في القائمة السوداء");
            }

            // فحص تكرار الهاتف إن وُجد
            if (!string.IsNullOrEmpty(cleanPhone))
            {
                var phoneExists = await _db.VisitorBlacklists
                    .AnyAsync(b => b.Phone == cleanPhone && b.IsActive);

                if (phoneExists)
                    throw new InvalidOperationException("رقم الهاتف هذا موجود مسبقاً في القائمة السوداء");
            }

            // رفع وحفظ ملف صورة الهوية إن وُجدت
            string? attachmentPath = null;
            if (dto.Attachment != null && dto.Attachment.Length > 0)
            {
                attachmentPath = await SaveFileAsync(dto.Attachment);
            }

            var entry = new VisitorBlacklist
            {
                FullName = dto.FullName.Trim(),
                NationalId = cleanNationalId,
                Phone = cleanPhone,
                Reason = dto.Reason,
                IncidentDate = dto.IncidentDate,
                IsPermanent = dto.IsPermanent,
                ExpiresAt = dto.IsPermanent ? null : dto.ExpiresAt,
                Notes = dto.Notes,
                CreatedBy = createdBy,
                AttachmentUrl = attachmentPath
            };

            _db.VisitorBlacklists.Add(entry);
            await _db.SaveChangesAsync();

            return (await GetByIdAsync(entry.Id))!;
        }

        public async Task<bool> RemoveAsync(int id)
        {
            var entry = await _db.VisitorBlacklists.FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
            if (entry == null) return false;

            // Soft Delete لرفع الحظر
            entry.IsActive = false;
            entry.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        // ===== محرك الفحص الأمني ثلاثي الأبعاد =====
        public async Task<CheckBlacklistResultDto> CheckVisitorAsync(string? phone, string? nationalId = null, string? fullName = null)
        {
            var cleanNationalId = nationalId?.Trim();
            var cleanPhone = phone?.Trim();
            var now = DateTime.Now;

            // 👈 الطبقة 1: التحقق بالرقم الوطني
            if (!string.IsNullOrEmpty(cleanNationalId))
            {
                var byNationalId = await _db.VisitorBlacklists
                    .FirstOrDefaultAsync(b => b.IsActive && b.NationalId == cleanNationalId);

                if (byNationalId != null)
                {
                    bool isBlocked = byNationalId.IsPermanent || (byNationalId.ExpiresAt.HasValue && byNationalId.ExpiresAt.Value >= now);
                    if (isBlocked)
                    {
                        return new CheckBlacklistResultDto
                        {
                            IsBlacklisted = true,
                            Reason = byNationalId.Reason,
                            BlockedSince = byNationalId.IncidentDate,
                            MatchType = "NationalId"
                        };
                    }
                }
            }

            // 👈 الطبقة 2: التحقق برقم الهاتف
            if (!string.IsNullOrEmpty(cleanPhone))
            {
                var byPhone = await _db.VisitorBlacklists
                    .FirstOrDefaultAsync(b => b.IsActive && b.Phone == cleanPhone);

                if (byPhone != null)
                {
                    bool isBlocked = byPhone.IsPermanent || (byPhone.ExpiresAt.HasValue && byPhone.ExpiresAt.Value >= now);
                    if (isBlocked)
                    {
                        return new CheckBlacklistResultDto
                        {
                            IsBlacklisted = true,
                            Reason = byPhone.Reason,
                            BlockedSince = byPhone.IncidentDate,
                            MatchType = "Phone"
                        };
                    }
                }
            }

            // 👈 الطبقة 3: فحص تطابق الاسم تقريبياً وعرض صورة الشخص المرفوعة للمطابقة العينية
            if (!string.IsNullOrEmpty(fullName))
            {
                var nameParts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (nameParts.Length >= 2)
                {
                    string firstName = nameParts[0];
                    string lastName = nameParts[^1];

                    var allBlocked = await _db.VisitorBlacklists
                        .Where(b => b.IsActive && (b.IsPermanent || (b.ExpiresAt.HasValue && b.ExpiresAt.Value >= now)))
                        .ToListAsync();

                    foreach (var blocked in allBlocked)
                    {
                        var blockedParts = blocked.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (blockedParts.Length >= 2)
                        {
                            string blockedFirst = blockedParts[0];
                            string blockedLast = blockedParts[^1];

                            // إذا تطابق الاسم الأول واللقب
                            if (firstName == blockedFirst && lastName == blockedLast)
                            {
                                return new CheckBlacklistResultDto
                                {
                                    IsBlacklisted = false, // تحذير فقط وليس حظراً تاماً
                                    Reason = $"⚠️ تحذير: الاسم شبيه باسم محظور وهو ({blocked.FullName}) - سبب الحظر: {blocked.Reason}. يرجى التحقق من صورة الهوية المرفقة.",
                                    BlockedSince = blocked.IncidentDate,
                                    MatchType = $"PartialName|{blocked.AttachmentUrl}" // تمرير رابط الهوية للتحقق بصرياً
                                };
                            }
                        }
                    }
                }
            }

            return new CheckBlacklistResultDto { IsBlacklisted = false };
        }

        private async Task<string> SaveFileAsync(IFormFile file)
        {
            string webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string uploadsFolder = Path.Combine(webRoot, "uploads", "blacklist");

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            string fileExtension = Path.GetExtension(file.FileName);
            string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/uploads/blacklist/{uniqueFileName}";
        }
    }
}