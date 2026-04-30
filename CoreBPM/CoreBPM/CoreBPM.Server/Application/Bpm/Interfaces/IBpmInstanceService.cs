using CoreBPM.Server.Application.Bpm.DTOs;

namespace CoreBPM.Server.Application.Bpm.Interfaces;

/// <summary>Сервис управления экземплярами бизнес-процессов.</summary>
public interface IBpmInstanceService
{
    /// <summary>Возвращает список экземпляров процесса.</summary>
    Task<IReadOnlyList<BpmInstanceListItemDto>> GetInstancesAsync(
        Guid processId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    /// <summary>Возвращает экземпляр по идентификатору.</summary>
    Task<BpmInstanceDto> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default);

    /// <summary>Создаёт (запускает) экземпляр процесса вручную.</summary>
    Task<BpmInstanceDto> CreateInstanceAsync(
        Guid processId,
        CreateInstanceRequest request,
        Guid initiatorUserId,
        CancellationToken ct = default);

    /// <summary>Создаёт экземпляр через внешний вебхук (проверяет токен).</summary>
    Task<BpmInstanceDto> CreateInstanceViaWebhookAsync(
        string webhookKey,
        WebhookLaunchRequest request,
        CancellationToken ct = default);

    /// <summary>Возвращает список заданий планировщика для процесса.</summary>
    Task<IReadOnlyList<BpmSchedulerJobDto>> GetSchedulerJobsAsync(Guid processId, CancellationToken ct = default);
}
