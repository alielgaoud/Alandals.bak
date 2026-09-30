using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Users;
using Andalos.API.Enums;
using Andalos.API.Helpers;
using Andalos.API.Interfaces;
using Andalos.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Andalos.API.Services
{
    public class PermissionPackageService : IPermissionPackageService
    {
        private readonly AppDbContext _db;

        public PermissionPackageService(AppDbContext db)
        {
            _db = db;
        }

        public Task<List<PermissionModuleDto>> GetModulesAsync()
        {
            var list = PermissionModules.ModuleDisplayNames
                .Select(x => new PermissionModuleDto
                {
                    ModuleKey = x.Key,
                    ModuleName = x.Value
                })
                .OrderBy(x => x.ModuleName)
                .ToList();

            return Task.FromResult(list);
        }

        public async Task<List<PermissionPackageResponseDto>> GetAllAsync()
        {
            var packages = await _db.PermissionPackages
                .Include(p => p.Items)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return packages.Select(MapToDto).ToList();
        }

        public async Task<PermissionPackageResponseDto?> GetByIdAsync(int id)
        {
            var package = await _db.PermissionPackages
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            return package == null ? null : MapToDto(package);
        }

        public async Task<PermissionPackageResponseDto> CreateAsync(
      CreatePermissionPackageDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new InvalidOperationException("اسم الباقة مطلوب");

            if (dto.PermissionKeys == null)
                throw new InvalidOperationException(
                    "يجب إرسال permissionKeys صراحة");

            var validKeys = Permissions.GetAllPermissions()
                .ToHashSet(StringComparer.Ordinal);

            var keys = dto.PermissionKeys
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (keys.Any(key => !validKeys.Contains(key)))
                throw new InvalidOperationException(
                    "تتضمن الباقة مفاتيح صلاحيات غير معروفة للخادم");

            var name = dto.Name.Trim();

            if (await _db.PermissionPackages.AnyAsync(
                    p => p.Name == name && p.IsActive))
            {
                throw new InvalidOperationException(
                    "اسم باقة الصلاحيات موجود مسبقاً");
            }

            var package = new PermissionPackage
            {
                Name = name,
                Description = dto.Description,
                IsActive = true
            };

            foreach (var key in keys)
            {
                package.Items.Add(new PermissionPackageItem
                {
                    PermissionKey = key
                });
            }

            _db.PermissionPackages.Add(package);
            await _db.SaveChangesAsync();

            return MapToDto(package);
        }
        public async Task<PermissionPackageResponseDto?> UpdateAsync(int id, UpdatePermissionPackageDto dto)
        {
            var package = await _db.PermissionPackages
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (package == null) return null;

            var nameExists = await _db.PermissionPackages
                .AnyAsync(p => p.Name == dto.Name && p.Id != id && p.IsActive);
            if (nameExists)
                throw new InvalidOperationException("اسم الصلاحية موجود مسبقاً");

            package.Name = dto.Name.Trim();
            package.Description = dto.Description;
            package.IsActive = dto.IsActive;
            package.UpdatedAt = DateTimeHelper.LibyaNow;

            // استبدال العناصر
            _db.PermissionPackageItems.RemoveRange(package.Items);

            var keys = ExpandModulesToKeys(dto.Modules);
            foreach (var key in keys.Distinct())
            {
                _db.PermissionPackageItems.Add(new PermissionPackageItem
                {
                    PackageId = package.Id,
                    PermissionKey = key
                });
            }

            await _db.SaveChangesAsync();

            // إعادة التحميل
            package = await _db.PermissionPackages.Include(p => p.Items).FirstAsync(p => p.Id == id);
            return MapToDto(package);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var package = await _db.PermissionPackages.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (package == null) return false;

            package.IsActive = false;
            package.UpdatedAt = DateTimeHelper.LibyaNow;
            await _db.SaveChangesAsync();
            return true;
        }

      

        public async Task<List<PermissionPackageResponseDto>> GetUserPackagesAsync(int userId)
        {
            var packages = await _db.UserPermissionPackages
                .Include(x => x.Package)!.ThenInclude(p => p!.Items)
                .Where(x => x.UserId == userId && x.IsActive && x.Package != null && x.Package.IsActive)
                .Select(x => x.Package!)
                .ToListAsync();

            return packages.Select(MapToDto).ToList();
        }

        public async Task<List<string>> GetEffectivePermissionsForUserAsync(
       int userId)
        {
            var direct = await _db.UserPermissions
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.IsActive)
                .Select(p => p.PermissionKey)
                .ToListAsync();

            var fromPackages = await _db.UserPermissionPackages
                .AsNoTracking()
                .Where(link =>
                    link.UserId == userId &&
                    link.IsActive &&
                    link.Package != null &&
                    link.Package.IsActive)
                .SelectMany(link =>
                    link.Package!.Items.Select(item => item.PermissionKey))
                .ToListAsync();

            return direct
                .Concat(fromPackages)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<bool> AssignPackagesToUserAsync(
       AssignPackagesToUserDto dto)
        {
            if (dto.PackageIds == null ||
                !dto.ExpectedVersion.HasValue ||
                dto.ExpectedVersion.Value < 0)
            {
                throw new InvalidOperationException(
                    "يجب إرسال packageIds و expectedVersion صراحة");
            }

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == dto.UserId && u.IsActive);

            if (user == null)
                return false;

            if (user.Role == UserRole.SuperAdmin ||
                user.UserName.Equals(
                    SystemConstants.SuperAdminUserName,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "لا يمكن تعديل باقات مدير النظام");
            }

            if (user.PermissionsVersion != dto.ExpectedVersion.Value)
                throw new PermissionVersionConflictException();

            var requested = dto.PackageIds
                .Distinct()
                .ToHashSet();

            var validIds = await _db.PermissionPackages
                .Where(p => requested.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            if (validIds.Count != requested.Count)
                throw new InvalidOperationException(
                    "إحدى الباقات غير موجودة أو غير نشطة");

            var existing = await _db.UserPermissionPackages
                .Where(link => link.UserId == dto.UserId)
                .ToListAsync();

            foreach (var link in existing)
            {
                if (requested.Contains(link.PackageId))
                    link.IsActive = true;
                else
                    _db.UserPermissionPackages.Remove(link);
            }

            var existingIds = existing
                .Select(link => link.PackageId)
                .ToHashSet();

            foreach (var packageId in requested.Except(existingIds))
            {
                _db.UserPermissionPackages.Add(
                    new UserPermissionPackage
                    {
                        UserId = dto.UserId,
                        PackageId = packageId,
                        IsActive = true
                    });
            }

            // لا تعديل إطلاقاً على _db.UserPermissions هنا.
            user.PermissionsVersion++;
            user.UpdatedAt = DateTimeHelper.LibyaNow;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _db.ChangeTracker.Clear();
                throw new PermissionVersionConflictException(ex);
            }

            return true;
        }


        private static List<string> ExpandModulesToKeys(List<string> modules)
        {
            var keys = new List<string>();
            foreach (var module in modules.Distinct())
            {
                if (PermissionModules.ModuleMap.TryGetValue(module, out var moduleKeys))
                    keys.AddRange(moduleKeys);
            }
            return keys;
        }

        private static PermissionPackageResponseDto MapToDto(PermissionPackage package)
        {
            var keys = package.Items.Select(i => i.PermissionKey).Distinct().ToList();

            // استنتاج modules من keys
            var modules = PermissionModules.ModuleMap
                .Where(m => m.Value.Any(k => keys.Contains(k)))
                .Select(m => m.Key)
                .ToList();

            return new PermissionPackageResponseDto
            {
                Id = package.Id,
                Name = package.Name,
                Description = package.Description,
                IsActive = package.IsActive,
                Modules = modules,
                ModuleNames = modules
                    .Select(m => PermissionModules.ModuleDisplayNames.GetValueOrDefault(m, m))
                    .ToList(),
                PermissionKeys = keys
            };
        }
    }
}