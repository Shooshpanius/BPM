using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Org.Interfaces;

/// <summary>Сервис управления деревом подразделений (публичный уровень).</summary>
public interface IOrgUnitsService
{
    /// <summary>
    /// Возвращает дерево подразделений организации.
    /// </summary>
    /// <param name="organizationId">Идентификатор организации.</param>
    /// <param name="status">Фильтр по статусу. Null — все статусы.</param>
    /// <param name="search">Поиск по части названия или кода. Ветка к найденным узлам раскрывается автоматически.</param>
    Task<IReadOnlyList<OrgUnitTreeDto>> GetTreeAsync(
        Guid organizationId,
        DepartmentStatus? status = null,
        string? search = null,
        CancellationToken ct = default);

    /// <summary>Возвращает подразделение по идентификатору с breadcrumb и счётчиками сотрудников.</summary>
    Task<OrgUnitDto> GetByIdAsync(Guid unitId, CancellationToken ct = default);

    /// <summary>Создаёт новое подразделение.</summary>
    Task<OrgUnitDto> CreateAsync(CreateUnitRequest request, Guid? callerUserId, CancellationToken ct = default);

    /// <summary>Обновляет данные подразделения (без изменения Path — используйте MoveAsync).</summary>
    Task<OrgUnitDto> UpdateAsync(Guid unitId, UpdateUnitRequest request, Guid? callerUserId, CancellationToken ct = default);

    /// <summary>Архивирует подразделение (мягкое удаление). Запрет при наличии активных сотрудников или потомков.</summary>
    Task ArchiveAsync(Guid unitId, Guid? callerUserId, CancellationToken ct = default);

    /// <summary>Перемещает подразделение в новый родительский узел, пересчитывая Path у всех потомков.</summary>
    Task<OrgUnitDto> MoveAsync(Guid unitId, MoveUnitRequest request, Guid? callerUserId, CancellationToken ct = default);

    /// <summary>Возвращает историю изменений подразделения.</summary>
    Task<IReadOnlyList<UnitHistoryDto>> GetHistoryAsync(Guid unitId, CancellationToken ct = default);
}
