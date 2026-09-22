using Andalos.API.DTOs.System;

namespace Andalos.API.Interfaces
{
    public interface ISystemResetService
    {
        Task<bool> ResetDatabaseToFactoryDefaultsAsync(ResetSystemDto dto, int currentUserId);
    }
}