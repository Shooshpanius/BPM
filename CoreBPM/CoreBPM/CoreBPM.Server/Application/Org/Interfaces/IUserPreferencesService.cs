using CoreBPM.Server.Application.Org.DTOs;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис настроек интерфейса пользователя (FR-ORG-02.3).</summary>
public interface IUserPreferencesService
{
    /// <summary>Возвращает настройки пользователя. Если запись не существует — создаёт с умолчаниями.</summary>
    Task<UserPreferencesDto> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Обновляет настройки пользователя.</summary>
    Task<UserPreferencesDto> UpdateAsync(Guid userId, UpdatePreferencesRequest req, CancellationToken ct = default);
}
