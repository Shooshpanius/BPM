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

    // ─── Управление состоянием ───────────────────────────────────────────────

    /// <summary>Прерывает (отменяет) экземпляр процесса с указанием причины.</summary>
    Task<BpmInstanceDto> CancelInstanceAsync(Guid instanceId, CancelInstanceRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Приостанавливает выполнение экземпляра.</summary>
    Task<BpmInstanceDto> SuspendInstanceAsync(Guid instanceId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Возобновляет приостановленный экземпляр.</summary>
    Task<BpmInstanceDto> ResumeInstanceAsync(Guid instanceId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Изменяет ответственного за экземпляр.</summary>
    Task<BpmInstanceDto> ChangeResponsibleAsync(Guid instanceId, ChangeResponsibleRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Обновляет значение переменной экземпляра.</summary>
    Task<BpmInstanceVariableDto> UpdateVariableAsync(Guid instanceId, string variableName, UpdateInstanceVariableRequest request, Guid actorUserId, CancellationToken ct = default);

    // ─── История ────────────────────────────────────────────────────────────

    /// <summary>Возвращает журнал событий экземпляра.</summary>
    Task<IReadOnlyList<BpmInstanceHistoryEntryDto>> GetHistoryAsync(Guid instanceId, CancellationToken ct = default);

    /// <summary>Добавляет комментарий или вопрос к экземпляру.</summary>
    Task<BpmInstanceHistoryEntryDto> AddCommentAsync(Guid instanceId, AddCommentRequest request, Guid actorUserId, CancellationToken ct = default);

    // ─── Участники ──────────────────────────────────────────────────────────

    /// <summary>Возвращает список участников экземпляра.</summary>
    Task<IReadOnlyList<BpmInstanceParticipantDto>> GetParticipantsAsync(Guid instanceId, CancellationToken ct = default);

    /// <summary>Добавляет участника к экземпляру.</summary>
    Task<BpmInstanceParticipantDto> AddParticipantAsync(Guid instanceId, AddParticipantRequest request, Guid actorUserId, CancellationToken ct = default);

    /// <summary>Удаляет участника из экземпляра.</summary>
    Task RemoveParticipantAsync(Guid instanceId, Guid participantUserId, Guid actorUserId, CancellationToken ct = default);
}
