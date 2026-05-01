using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления очередью исполнения (FR-BPM-02.5).</summary>
public interface IBpmQueueService
{
    /// <summary>Возвращает список заданий очереди с фильтрацией и пагинацией.</summary>
    Task<IReadOnlyList<BpmExecutionJobDto>> GetQueueAsync(
        BpmJobStatus? status,
        string? instanceName,
        Guid? processId,
        bool includeScheduled,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Возвращает агрегированные счётчики по статусам очереди.</summary>
    Task<QueueStatsDto> GetQueueStatsAsync(CancellationToken ct = default);

    /// <summary>Принудительно повторяет задание (сбрасывает статус на Pending).</summary>
    Task RetryJobAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Отменяет таймерное задание.</summary>
    Task CancelTimerAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Переносит время запуска таймерного задания.</summary>
    Task RescheduleTimerAsync(Guid jobId, DateTimeOffset newRunAt, CancellationToken ct = default);

    /// <summary>Возвращает аналитику выполнения узлов процесса за период.</summary>
    Task<IReadOnlyList<NodeAnalyticsDto>> GetNodeAnalyticsAsync(
        Guid processId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}
