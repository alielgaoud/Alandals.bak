using Andalos.API.Constants;
using Andalos.API.Data;
using Andalos.API.DTOs.Users;
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

        public async Task<PermissionPackageResponseDto> CreateAsync(CreatePermissionPackageDto dto)
        {
            var nameExists = await _db.PermissionPackages.AnyAsync(p => p.Name == dto.Name && p.IsActive);
            if (nameExists)
                throw new InvalidOperationException("اسم الصلاحية موجود مسبقاً");

            var keys = ExpandModulesToKeys(dto.Modules);

            var package = new PermissionPackage
            {
                Name = dto.Name.Trim(),
                Description = dto.Description,
                IsActive = true
            };

            foreach (var key in keys.Distinct())
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
            package.UpdatedAt = DateTime.UtcNow;

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
            package.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AssignPackagesToUserAsync(AssignPackagesToUserDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == dto.UserId && u.IsActive);
            if (user == null) return false;

            // حماية superadmin
            if (user.UserName.Equals(SystemConstants.SuperAdminUserName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("لا يمكن تعديل صلاحيات مدير النظام الرئيسي");

            var old = await _db.UserPermissionPackages.Where(x => x.UserId == dto.UserId).ToListAsync();
            _db.UserPermissionPackages.RemoveRange(old);

            var validPackageIds = await _db.PermissionPackages
                .Where(p => dto.PackageIds.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var packageId in validPackageIds)
            {
                _db.UserPermissionPackages.Add(new UserPermissionPackage
                {
                    UserId = dto.UserId,
                    PackageId = packageId
                });
            }

            // مزامنة اختيارية مع UserPermissions (للتوافق مع النظام الحالي)
            await SyncUserPermissionsFromPackagesAsync(dto.UserId);

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

        public async Task<List<string>> GetEffectivePermissionsForUserAsync(int userId)
        {
            // صلاحيات مباشرة قديمة
            var direct = await _db.UserPermissions
                .Where(p => p.UserId == userId && p.IsActive)
                .Select(p => p.PermissionKey)
                .ToListAsync();

            // صلاحيات من الباقات
            var fromPackages = await _db.UserPermissionPackages
                .Where(x => x.UserId == userId && x.IsActive)
                .SelectMany(x => x.Package!.Items.Select(i => i.PermissionKey))
                .ToListAsync();

            return direct.Concat(fromPackages).Distinct().ToList();
        }

        private async Task SyncUserPermissionsFromPackagesAsync(int userId)
        {
            var packageKeys = await _db.UserPermissionPackages
                .Where(x => x.UserId == userId && x.IsActive)
                .SelectMany(x => x.Package!.Items.Select(i => i.PermissionKey))
                .Distinct()
                .ToListAsync();

            var oldPerms = await _db.UserPermissions.Where(p => p.UserId == userId).ToListAsync();
            _db.UserPermissions.RemoveRange(oldPerms);

            foreach (var key in packageKeys)
            {
                _db.UserPermissions.Add(new UserPermission
                {
                    UserId = userId,
                    PermissionKey = key
                });
            }
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