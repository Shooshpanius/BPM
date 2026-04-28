using CoreBPM.Server.Application.Org.DTOs;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис управления назначениями пользователей на должности (FR-ORG-01.3).</summary>
public interface IOrgAssignmentService
{
    /// <summary>
    /// Возвращает список назначений с опциональной фильтрацией.
    /// </summary>
    Task<IReadOnlyList<AssignmentDto>> GetAllAsync(
        Guid? userId = null,
        Guid? positionId = null,
        Guid? organizationId = null,
        bool? activeOnly = null,
        CancellationToken ct = default);

    /// <summary>Возвращает назначение по идентификатору.</summary>
    Task<AssignmentDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Создаёт назначение пользователя на должность.
    /// При IsPrimary=true проверяет отсутствие другого активного основного назначения.
    /// Автоматически применяет матрицу ролей должности (OrgPositionRoleMapping).
    /// </summary>
    Task<AssignmentDto> CreateAsync(CreateAssignmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Изменяет параметры назначения (ставку, тип, даты).
    /// Пересчитывает роли пользователя при необходимости.
    /// </summary>
    Task<AssignmentDto> UpdateAsync(Guid id, UpdateAssignmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Завершает назначение: устанавливает EndDate = сегодня (если ещё не задана).
    /// Снимает роли должности, если у пользователя больше нет других активных назначений с теми же ролями.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
