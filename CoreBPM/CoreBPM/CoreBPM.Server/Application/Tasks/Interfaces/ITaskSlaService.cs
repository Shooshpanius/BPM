using CoreBPM.Server.Application.Tasks.DTOs;

namespace CoreBPM.Server.Application.Tasks.Interfaces;

/// <summary>Сервис SLA-правил задач (FR-TASK-01.2).</summary>
public interface ITaskSlaService
{
    /// <summary>Возвращает список всех SLA-правил.</summary>
    Task<IReadOnlyList<TaskSlaRuleDto>> ListAsync(CancellationToken ct = default);

    /// <summary>Создаёт SLA-правило.</summary>
    Task<TaskSlaRuleDto> CreateAsync(UpsertTaskSlaRuleRequest req, CancellationToken ct = default);

    /// <summary>Обновляет SLA-правило.</summary>
    Task<TaskSlaRuleDto> UpdateAsync(Guid id, UpsertTaskSlaRuleRequest req, CancellationToken ct = default);

    /// <summary>Удаляет SLA-правило.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Вычисляет DueDate по правилу SLA для задачи с указанной категорией и приоритетом.
    /// Возвращает null, если подходящее правило не найдено.
    /// </summary>
    Task<DateTimeOffset?> ComputeDueDateAsync(
        string? categoryId,
        Domain.Tasks.TaskPriority priority,
        DateTimeOffset from,
        CancellationToken ct = default);
}
