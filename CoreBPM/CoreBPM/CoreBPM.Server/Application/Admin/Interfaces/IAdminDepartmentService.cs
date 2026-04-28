using CoreBPM.Server.Application.Admin.DTOs;

namespace CoreBPM.Server.Application.Admin.Interfaces;

/// <summary>Сервис управления подразделениями организаций.</summary>
public interface IAdminDepartmentService
{
    /// <summary>
    /// Возвращает плоский список подразделений.
    /// Если передан <paramref name="organizationId"/> — фильтрует по организации.
    /// </summary>
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid? organizationId = null, CancellationToken ct = default);

    /// <summary>Возвращает иерархическое дерево подразделений организации.</summary>
    Task<IReadOnlyList<DepartmentTreeDto>> GetTreeAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>Возвращает подразделение по идентификатору.</summary>
    Task<DepartmentDto> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Создаёт новое подразделение в указанной организации.</summary>
    Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct = default);

    /// <summary>Обновляет данные подразделения (название, описание, родитель, статус).</summary>
    Task<DepartmentDto> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken ct = default);

    /// <summary>Архивирует подразделение (мягкое удаление). Запрещено, если есть активные дочерние подразделения или активные сотрудники.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
