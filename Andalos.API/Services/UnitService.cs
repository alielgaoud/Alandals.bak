using Andalos.API.Data;
using Andalos.API.DTOs.Units;
using Andalos.API.Enums;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class UnitService : IUnitService
    {
        private readonly AppDbContext _db;

        public UnitService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<List<UnitResponseDto>> GetAllAsync()
        {
            return await _db.Units
                .Where(u => u.IsActive)
                .OrderBy(u => u.UnitNumber)
                .Select(u => MapToDto(u))
                .ToListAsync();
        }

        public async Task<UnitResponseDto?> GetByIdAsync(int id)
        {
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            return unit == null ? null : MapToDto(unit);
        }

        public async Task<UnitResponseDto> CreateAsync(CreateUnitDto dto)
        {
            var exists = await _db.Units
                .AnyAsync(u => u.UnitNumber == dto.UnitNumber && u.IsActive);

            if (exists)
                throw new InvalidOperationException($"المحل رقم {dto.UnitNumber} موجود مسبقاً");

            var unit = new Unit
            {
                UnitNumber = dto.UnitNumber,
                UnitName = null, // 👈 الاسم يتحدد لاحقاً مع المستأجر
                ActivityType = dto.ActivityType, // 👈 نوع النشاط
                Status = UnitStatus.Vacant,
                Area = dto.Area,
                Floor = dto.Floor,
                Building = dto.Building,
                Description = dto.Description,
                Notes = dto.Notes,
                ElectricityMeterStart = dto.ElectricityMeterStart
            };

            _db.Units.Add(unit);
            await _db.SaveChangesAsync();

            return MapToDto(unit);
        }

        public async Task<UnitResponseDto?> UpdateAsync(int id, UpdateUnitDto dto)
        {
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            if (unit == null) return null;

            unit.UnitName = dto.UnitName; // 👈 يمكن تغييره عند تغيير المستأجر
            unit.ActivityType = dto.ActivityType; // 👈 نوع النشاط
            unit.Status = dto.Status;
            unit.Area = dto.Area;
            unit.Floor = dto.Floor;
            unit.Building = dto.Building;
            unit.Description = dto.Description;
            unit.Notes = dto.Notes;
            unit.ElectricityMeterStart = dto.ElectricityMeterStart;
            unit.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return MapToDto(unit);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var unit = await _db.Units
                .FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

            if (unit == null) return false;

            unit.IsActive = false;
            unit.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return true;
        }

        public async Task<int> GetCountByStatusAsync(string status)
        {
            if (Enum.TryParse<UnitStatus>(status, true, out var parsedStatus))
            {
                return await _db.Units
                    .CountAsync(u => u.Status == parsedStatus && u.IsActive);
            }
            return 0;
        }

        private static UnitResponseDto MapToDto(Unit u)
        {
            return new UnitResponseDto
            {
                Id = u.Id,
                UnitNumber = u.UnitNumber,
                UnitName = u.UnitName,
                ActivityType = u.ActivityType.ToString(), // 👈 نوع النشاط
                Status = u.Status.ToString(),
                Area = u.Area,
                Floor = u.Floor,
                Building = u.Building,
                Description = u.Description,
                Notes = u.Notes,
                ElectricityMeterStart = u.ElectricityMeterStart,
                CreatedAt = u.CreatedAt
            };
        }
    }
}