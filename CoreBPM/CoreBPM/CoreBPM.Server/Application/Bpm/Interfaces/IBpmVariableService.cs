using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления переменными контекста бизнес-процесса.</summary>
public interface IBpmVariableService
{
    /// <summary>Возвращает список переменных процесса, упорядоченный по SortOrder.</summary>
    Task<IReadOnlyList<BpmProcessVariableDto>> GetVariablesAsync(Guid processId, CancellationToken ct = default);

    /// <summary>Создаёт новую переменную.</summary>
    Task<BpmProcessVariableDto> CreateVariableAsync(Guid processId, CreateBpmVariableRequest request, CancellationToken ct = default);

    /// <summary>Обновляет переменную.</summary>
    Task<BpmProcessVariableDto> UpdateVariableAsync(Guid processId, Guid variableId, UpdateBpmVariableRequest request, CancellationToken ct = default);

    /// <summary>Удаляет переменную.</summary>
    Task DeleteVariableAsync(Guid processId, Guid variableId, CancellationToken ct = default);

    /// <summary>Изменяет порядок переменных (batch reorder).</summary>
    Task ReorderVariablesAsync(Guid processId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
