using CoreBPM.Server.Application.Rules.DTOs;

namespace CoreBPM.Server.Application.Rules.Interfaces;

/// <summary>Сервис управления DMN-таблицами бизнес-правил.</summary>
public interface IDmnService
{
    // ─── CRUD таблиц ───────────────────────────────────────────────────────────

    /// <summary>Возвращает список всех DMN-таблиц.</summary>
    Task<IReadOnlyList<DmnTableListItemDto>> GetTablesAsync(CancellationToken ct = default);

    /// <summary>Возвращает таблицу по идентификатору.</summary>
    Task<DmnTableDto> GetTableByIdAsync(Guid tableId, CancellationToken ct = default);

    /// <summary>Создаёт новую таблицу и первый черновик без строк.</summary>
    Task<DmnTableDto> CreateTableAsync(CreateDmnTableRequest request, CancellationToken ct = default);

    /// <summary>Обновляет метаданные таблицы (название, описание, хит-политика).</summary>
    Task<DmnTableDto> UpdateTableAsync(Guid tableId, UpdateDmnTableRequest request, CancellationToken ct = default);

    /// <summary>Удаляет таблицу. Запрещено, если есть опубликованные версии.</summary>
    Task DeleteTableAsync(Guid tableId, CancellationToken ct = default);

    // ─── Версионирование ───────────────────────────────────────────────────────

    /// <summary>Возвращает список всех версий таблицы (без схемы).</summary>
    Task<IReadOnlyList<DmnTableVersionInfoDto>> GetVersionsAsync(Guid tableId, CancellationToken ct = default);

    /// <summary>Возвращает полную схему указанной версии.</summary>
    Task<DmnTableVersionDto> GetVersionAsync(Guid tableId, Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Сохраняет новый черновик на основе переданной схемы.
    /// Предыдущие черновики остаются в истории.
    /// </summary>
    Task<DmnTableVersionDto> SaveDraftAsync(Guid tableId, SaveDmnTableVersionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Публикует указанную версию.
    /// Предыдущая опубликованная версия переходит в статус Archived.
    /// </summary>
    Task<DmnTableVersionInfoDto> PublishVersionAsync(Guid tableId, Guid versionId, CancellationToken ct = default);

    // ─── Тестирование ─────────────────────────────────────────────────────────

    /// <summary>Выполняет тестирование версии таблицы на заданных входных значениях.</summary>
    Task<DmnTestResponse> EvaluateAsync(Guid tableId, Guid versionId, DmnTestRequest request, CancellationToken ct = default);
}
