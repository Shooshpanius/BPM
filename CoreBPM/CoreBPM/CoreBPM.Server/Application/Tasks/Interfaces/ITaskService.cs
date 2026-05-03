using CoreBPM.Server.Application.Tasks.DTOs;

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
}
