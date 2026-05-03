using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Domain.Tasks;

namespace CoreBPM.Server.Application.Tasks.Interfaces;

/// <summary>Сервис управления задачами (FR-TASK-01.1, FR-TASK-01.2).</summary>
public interface ITaskService
{
    Task<TaskDto> CreateAsync(CreateTaskRequest req, Guid authorId, CancellationToken ct = default);
    Task<TaskDto> GetAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskSummaryDto>> ListAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest req, Guid actorId, CancellationToken ct = default);
    Task DeleteAsync(Guid taskId, Guid actorId, CancellationToken ct = default);
    Task<TaskDto> CopyAsync(Guid taskId, Guid actorId, CancellationToken ct = default);
    Task MarkReadAsync(Guid taskId, Guid userId, CancellationToken ct = default);
    Task<TaskDto> ReassignAsync(Guid taskId, ReassignTaskRequest req, Guid actorId, CancellationToken ct = default);
    Task<TaskDto> CreateSubtaskAsync(Guid parentTaskId, CreateTaskRequest req, Guid authorId, CancellationToken ct = default);
    Task<TaskCommentDto> AddCommentAsync(Guid taskId, AddTaskCommentRequest req, Guid authorId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskCommentDto>> GetCommentsAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskAttachmentDto> AddAttachmentAsync(Guid taskId, AddTaskAttachmentRequest req, Guid uploadedBy, CancellationToken ct = default);
    Task<IReadOnlyList<TaskAttachmentDto>> GetAttachmentsAsync(Guid taskId, CancellationToken ct = default);
    Task<TaskParticipantDto> AddParticipantAsync(Guid taskId, AddTaskParticipantRequest req, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskParticipantDto>> GetParticipantsAsync(Guid taskId, CancellationToken ct = default);
    Task RemoveParticipantAsync(Guid taskId, Guid participantId, CancellationToken ct = default);
    Task<TaskRelationDto> AddRelationAsync(Guid taskId, AddTaskRelationRequest req, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskRelationDto>> GetRelationsAsync(Guid taskId, CancellationToken ct = default);
    Task RemoveRelationAsync(Guid taskId, Guid relationId, Guid actorId, CancellationToken ct = default);
    Task<TaskTagResultDto> AddTagAsync(Guid taskId, AddTaskTagRequest req, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetTagsAsync(Guid taskId, CancellationToken ct = default);
    Task RemoveTagAsync(Guid taskId, Guid tagId, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskHistoryEntryDto>> GetHistoryAsync(Guid taskId, CancellationToken ct = default);
    Task<byte[]> ExportToCsvAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<TaskSavedFilterDto>> GetSavedFiltersAsync(Guid userId, CancellationToken ct = default);
    Task<TaskSavedFilterDto> CreateSavedFilterAsync(Guid userId, CreateTaskSavedFilterRequest req, CancellationToken ct = default);
    Task DeleteSavedFilterAsync(Guid filterId, Guid userId, CancellationToken ct = default);
    Task<TaskTemplateDto> CreateTemplateAsync(CreateTaskTemplateRequest req, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskTemplateDto>> ListTemplatesAsync(Guid userId, CancellationToken ct = default);
    Task<TaskTemplateDto> UpdateTemplateAsync(Guid templateId, CreateTaskTemplateRequest req, Guid userId, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid templateId, Guid userId, CancellationToken ct = default);

    // --- FR-TASK-01.2: Действия по статусам ---

    /// <summary>Возвращает список допустимых действий для актора над задачей.</summary>
    Task<IReadOnlyList<TaskAllowedActionDto>> GetAllowedActionsAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Начать работу: New/Read → InProgress. Доступно исполнителю, соисполнителю, Admin.</summary>
    Task<TaskDto> StartWorkAsync(Guid taskId, Guid actorId, CancellationToken ct = default);

    /// <summary>Сделано: InProgress → Done / DoneNeedsControl. Доступно исполнителю, соисполнителю, Admin.</summary>
    Task<TaskDto> MarkDoneAsync(Guid taskId, Guid actorId, CancellationToken ct = default);

    /// <summary>Невозможно выполнить: InProgress → CannotDo / CannotDoNeedsControl. Доступно исполнителю, соисполнителю, Admin.</summary>
    Task<TaskDto> MarkCannotDoAsync(Guid taskId, Guid actorId, CancellationToken ct = default);

    /// <summary>Закрыть (отменить): не-финальный → Closed. Доступно автору и Admin.</summary>
    Task<TaskDto> CloseAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Отложить: не-финальный → Postponed + PostponedUntil. Доступно исполнителю и Admin.</summary>
    Task<TaskDto> PostponeAsync(Guid taskId, PostponeTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Принять контроль: DoneNeedsControl/CannotDoNeedsControl → *Controlled. Доступно контролёру и Admin.</summary>
    Task<TaskDto> AcceptControlAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Вернуть на доработку: DoneNeedsControl/CannotDoNeedsControl → New. Доступно контролёру и Admin.</summary>
    Task<TaskDto> ReturnToWorkAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    // ─── FR-TASK-01.3: Согласование ──────────────────────────────────────────

    /// <summary>Согласовать предварительное согласование: PreApproval → New. Доступно согласующему и Admin.</summary>
    Task<TaskDto> ApprovePreAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Отказать в предварительном согласовании: PreApproval → PreApprovalRejected. Доступно согласующему и Admin.</summary>
    Task<TaskDto> RejectPreAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Отправить задачу на согласование: New/Read/InProgress → OnApproval. Доступно исполнителю и Admin.</summary>
    Task<TaskDto> SendForApprovalAsync(Guid taskId, SendForApprovalRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Согласовать (от исполнителя): OnApproval → New. Доступно согласующему и Admin.</summary>
    Task<TaskDto> ApproveAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Отказать в согласовании (от исполнителя): OnApproval → ApprovalRejected. Доступно согласующему и Admin.</summary>
    Task<TaskDto> RejectAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Получить текущее состояние согласования задачи.</summary>
    Task<TaskApprovalStateDto> GetApprovalStateAsync(Guid taskId, CancellationToken ct = default);

    // ─── FR-TASK-01.4: Контроль и трудозатраты ───────────────────────────────

    /// <summary>Изменить контролёра и/или тип контроля после создания задачи. Доступно автору, контролёру и Admin.</summary>
    Task<TaskDto> UpdateControlAsync(Guid taskId, UpdateControlRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Взять задачу на текущий контроль. Устанавливает актора контролёром с типом CurrentControl.</summary>
    Task<TaskDto> TakeControlAsync(Guid taskId, Guid actorId, CancellationToken ct = default);

    /// <summary>Снять задачу с контроля. Доступно текущему контролёру и Admin.</summary>
    Task<TaskDto> ReleaseControlAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Добавить трудозатраты к задаче.</summary>
    Task<TaskTimeLogDto> AddTimeLogAsync(Guid taskId, AddTimeLogRequest req, Guid userId, CancellationToken ct = default);

    /// <summary>Получить журнал трудозатрат по задаче.</summary>
    Task<IReadOnlyList<TaskTimeLogDto>> GetTimeLogsAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Удалить запись трудозатрат. Доступно автору записи и Admin.</summary>
    Task DeleteTimeLogAsync(Guid taskId, Guid logId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    // ─── FR-TASK-01.5: Типы задач ─────────────────────────────────────────────

    /// <summary>Создать периодическую задачу с конфигурацией серии (FR-TASK-01.5.1).</summary>
    Task<TaskDto> CreatePeriodicTaskAsync(CreatePeriodicTaskRequest req, Guid authorId, CancellationToken ct = default);

    /// <summary>Обновить конфигурацию серии периодических задач (FR-TASK-01.5.1).</summary>
    Task<TaskRecurrenceDto> UpdateSeriesAsync(Guid rootTaskId, UpdateSeriesRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Остановить серию периодических задач (FR-TASK-01.5.1).</summary>
    /// <param name="activeTaskAction">Действие с активными экземплярами: null — оставить; "ForceComplete" — завершить; "Delete" — удалить.</param>
    Task StopSeriesAsync(Guid rootTaskId, Guid actorId, bool isAdmin, string? activeTaskAction = null, CancellationToken ct = default);

    /// <summary>Получить список экземпляров серии периодических задач (FR-TASK-01.5.1).</summary>
    Task<IReadOnlyList<PeriodicSeriesItemDto>> GetSeriesItemsAsync(Guid rootTaskId, bool activeOnly, CancellationToken ct = default);

    /// <summary>Создать задачу по резолюции документа (FR-TASK-01.5.3).</summary>
    Task<TaskDto> CreateResolutionTaskAsync(CreateResolutionTaskRequest req, Guid authorId, CancellationToken ct = default);

    /// <summary>Получить все задачи по резолюции, связанные с документом (FR-TASK-01.5.3).</summary>
    Task<IReadOnlyList<TaskDto>> GetDocumentResolutionsAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>Получить детали процесса для задачи вида ProcessTask (FR-TASK-01.5.2).</summary>
    Task<ProcessTaskInfoDto?> GetProcessTaskInfoAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Скачать все вложения задачи архивом ZIP (FR-TASK-01.5.2).</summary>
    Task<(Stream ZipStream, string FileName)> DownloadAttachmentsZipAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Создать следующий экземпляр в серии периодических задач (вызывается воркером).</summary>
    Task<TaskItem?> CreateNextPeriodicInstanceAsync(Guid recurrenceId, CancellationToken ct = default);
}
