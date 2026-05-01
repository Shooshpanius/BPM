using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;

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

    // ─── Мои процессы ────────────────────────────────────────────────────────

    /// <summary>Возвращает экземпляры процессов, в которых участвует указанный пользователь, с применением фильтра.</summary>
    Task<MyInstancesResult> GetMyInstancesAsync(
        Guid userId,
        MyInstancesFilter filter,
        int page = 1,
        int pageSize = 30,
        CancellationToken ct = default);

    /// <summary>Экспортирует «Мои процессы» в CSV-файл (UTF-8 BOM), применяя тот же фильтр без пагинации.</summary>
    Task<byte[]> ExportMyInstancesToCsvAsync(
        Guid userId,
        MyInstancesFilter filter,
        CancellationToken ct = default);

    // ─── Пакетный запуск (FR-BPM-02.1) ──────────────────────────────────────

    /// <summary>Запускает несколько экземпляров одного процесса одной операцией.</summary>
    Task<BatchLaunchResult> BatchCreateInstancesAsync(
        Guid processId,
        BatchLaunchRequest request,
        Guid initiatorUserId,
        CancellationToken ct = default);

    // ─── Переключение версии (FR-BPM-02.2) ───────────────────────────────────

    /// <summary>Переключает работающий экземпляр на другую опубликованную версию того же процесса.</summary>
    Task<BpmInstanceDto> SwitchVersionAsync(
        Guid instanceId,
        SwitchInstanceVersionRequest request,
        Guid actorUserId,
        CancellationToken ct = default);
}
