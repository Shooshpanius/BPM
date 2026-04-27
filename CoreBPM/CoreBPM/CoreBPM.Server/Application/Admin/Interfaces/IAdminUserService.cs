using CoreBPM.Server.Application.Admin.DTOs;

namespace CoreBPM.Server.Application.Admin.Interfaces;

/// <summary>Сервис управления пользователями в административной панели.</summary>
public interface IAdminUserService
{
    /// <summary>Возвращает список всех пользователей системы.</summary>
    Task<IReadOnlyList<AdminUserListItemDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Возвращает пользователя по идентификатору.</summary>
    Task<AdminUserListItemDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Создаёт пользователя: профиль (org_users) + учётную запись (auth_accounts).</summary>
    Task<AdminUserListItemDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    /// <summary>Обновляет профиль пользователя.</summary>
    Task<AdminUserListItemDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>Деактивирует пользователя (мягкое удаление).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
