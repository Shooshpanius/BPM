using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Domain.Tasks;

namespace CoreBPM.Server.Application.Tasks.Interfaces;

/// <summary>Сервис управления задачами (FR-TASK-01.1, FR-TASK-01.2).</summary>
public interface ITaskService
{
    Task<TaskDto> CreateAsync(CreateTaskRequest req, Guid authorId, CancellationToken ct = default);
    Task<TaskDto> GetAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskSummaryDto>> ListAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default);
    Task<TaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);
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
    Task<TaskDto> StartWorkAsync(Guid taskId, StartWorkRequest? req, Guid actorId, CancellationToken ct = default);

    /// <summary>Сделано: InProgress → Done / DoneNeedsControl. Доступно исполнителю, соисполнителю, Admin.</summary>
    Task<TaskDto> MarkDoneAsync(Guid taskId, MarkDoneRequest? req, Guid actorId, CancellationToken ct = default);

    /// <summary>Невозможно выполнить: InProgress → CannotDo / CannotDoNeedsControl. Доступно исполнителю, соисполнителю, Admin.</summary>
    Task<TaskDto> MarkCannotDoAsync(Guid taskId, MarkCannotDoRequest? req, Guid actorId, CancellationToken ct = default);

    /// <summary>Закрыть (отменить): не-финальный → Closed. Доступно автору и Admin.</summary>
    Task<TaskDto> CloseAsync(Guid taskId, CloseTaskRequest? req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Отложить: не-финальный → Postponed + PostponedUntil. Доступно исполнителю и Admin.</summary>
    Task<TaskDto> PostponeAsync(Guid taskId, PostponeTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Принять контроль: DoneNeedsControl/CannotDoNeedsControl → *Controlled. Доступно контролёру и Admin.</summary>
    Task<TaskDto> AcceptControlAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Вернуть на доработку: DoneNeedsControl/CannotDoNeedsControl → New. Доступно контролёру и Admin.</summary>
    Task<TaskDto> ReturnToWorkAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    // ─── FR-TASK-02.1: Дополнительные действия ───────────────────────────────

    /// <summary>Перенести срок задачи без смены статуса. Доступно автору, контролёру и Admin.</summary>
    Task<TaskDto> RescheduleAsync(Guid taskId, RescheduleTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Открыть заново: финальные/контролируемые статусы → New. Доступно контролёру, автору и Admin.</summary>
    Task<TaskDto> ReopenAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Взять задачу из очереди роли на себя. Доступно любому участнику, кроме текущего исполнителя.</summary>
    Task<TaskDto> ClaimAsync(Guid taskId, Guid actorId, CancellationToken ct = default);

    // ─── FR-TASK-02.1: Наблюдатели ───────────────────────────────────────────

    /// <summary>Получить список наблюдателей задачи.</summary>
    Task<IReadOnlyList<TaskParticipantDto>> GetWatchersAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Добавить наблюдателя к задаче.</summary>
    Task<TaskParticipantDto> AddWatcherAsync(Guid taskId, Guid watcherUserId, Guid actorId, CancellationToken ct = default);

    /// <summary>Удалить наблюдателя из задачи.</summary>
    Task RemoveWatcherAsync(Guid taskId, Guid watcherUserId, Guid actorId, bool isAdmin, CancellationToken ct = default);

    // ─── FR-TASK-02.1: Вопросы ───────────────────────────────────────────────

    /// <summary>Получить список вопросов по задаче.</summary>
    Task<IReadOnlyList<TaskQuestionDto>> GetQuestionsAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Задать вопрос по задаче. Получатель получает уведомление.</summary>
    Task<TaskQuestionDto> AskQuestionAsync(Guid taskId, AskTaskQuestionRequest req, Guid authorId, CancellationToken ct = default);

    /// <summary>Ответить на вопрос по задаче.</summary>
    Task<TaskQuestionDto> AnswerQuestionAsync(Guid taskId, Guid questionId, AnswerTaskQuestionRequest req, Guid actorId, CancellationToken ct = default);

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

    /// <summary>Массово подтвердить выполнение задач (принять контроль по нескольким задачам, FR-TASK-01.4).</summary>
    Task<int> BulkVerifyAsync(IReadOnlyList<Guid> taskIds, Guid actorId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Переназначить открытые ProcessTask-задачи при блокировке исполнителя (FR-TASK-01.5.2).</summary>
    Task ReassignBlockedProcessTasksAsync(Guid blockedUserId, CancellationToken ct = default);

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

    // ─── FR-TASK-02.3: Поиск, подписка, уведомления и календарь ─────────────

    /// <summary>Получить напоминания пользователя по задаче (FR-TASK-02.3).</summary>
    Task<IReadOnlyList<TaskReminderDto>> GetRemindersAsync(Guid taskId, Guid userId, CancellationToken ct = default);

    /// <summary>Добавить напоминание по задаче (FR-TASK-02.3).</summary>
    Task<TaskReminderDto> AddReminderAsync(Guid taskId, AddTaskReminderRequest req, Guid userId, CancellationToken ct = default);

    /// <summary>Удалить напоминание (FR-TASK-02.3).</summary>
    Task DeleteReminderAsync(Guid reminderId, Guid actorId, CancellationToken ct = default);

    /// <summary>Запланировать задачу на конкретное время (FR-TASK-02.3).</summary>
    Task<TaskDto> ScheduleTaskAsync(Guid taskId, ScheduleTaskRequest req, Guid actorId, CancellationToken ct = default);

    /// <summary>Снять задачу с планирования в календаре (FR-TASK-02.3).</summary>
    Task<TaskDto> UnscheduleTaskAsync(Guid taskId, Guid actorId, CancellationToken ct = default);

    /// <summary>Получить дашборд задач текущего пользователя (FR-TASK-02.3).</summary>
    Task<TaskDashboardDto> GetDashboardAsync(Guid userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Получить настройки уведомлений пользователя по задачам (FR-TASK-02.3).</summary>
    Task<IReadOnlyList<UserTaskNotificationSettingsDto>> GetNotificationSettingsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Обновить настройки уведомлений пользователя (FR-TASK-02.3).</summary>
    Task<IReadOnlyList<UserTaskNotificationSettingsDto>> UpdateNotificationSettingsAsync(Guid userId, IReadOnlyList<UpdateNotificationSettingRequest> settings, CancellationToken ct = default);

    /// <summary>Получить счётчики задач для бейджей в Sidebar (FR-TASK-02.2).</summary>
    Task<TaskCountersDto> GetCountersAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Экспортировать задачи в Excel-файл (FR-TASK-02.2).</summary>
    Task<byte[]> ExportToExcelAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default);
}
