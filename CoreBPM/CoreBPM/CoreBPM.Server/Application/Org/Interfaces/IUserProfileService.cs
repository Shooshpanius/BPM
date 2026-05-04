using CoreBPM.Server.Application.Org.DTOs;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис профиля пользователя (FR-ORG-02.1).</summary>
public interface IUserProfileService
{
    /// <summary>Возвращает профиль пользователя. Скрывает дату рождения согласно BirthDateVisibility.</summary>
    Task<UserProfileDto?> GetProfileAsync(Guid userId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Обновляет профиль пользователя. Обычный пользователь может редактировать только свой профиль.</summary>
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, Guid actorId, UpdateProfileRequest req, bool isAdmin, CancellationToken ct = default);

    /// <summary>Устанавливает URL аватара пользователя.</summary>
    Task<string> SetAvatarUrlAsync(Guid userId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Удаляет аватар пользователя.</summary>
    Task DeleteAvatarAsync(Guid userId, Guid actorId, bool isAdmin, CancellationToken ct = default);
}
