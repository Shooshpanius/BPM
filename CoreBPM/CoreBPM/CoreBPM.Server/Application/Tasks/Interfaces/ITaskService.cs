using CoreBPM.Server.Application.Tasks.DTOs;

namespace CoreBPM.Server.Application.Tasks.Interfaces;

/// <summary>Сервис управления задачами (FR-TASK-01.1).</summary>
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
    Task<TaskParticipantDto> AddParticipantAsync(Guid taskId, AddTaskParticipantRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<TaskParticipantDto>> GetParticipantsAsync(Guid taskId, CancellationToken ct = default);
    Task RemoveParticipantAsync(Guid taskId, Guid participantId, CancellationToken ct = default);
    Task<TaskRelationDto> AddRelationAsync(Guid taskId, AddTaskRelationRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<TaskRelationDto>> GetRelationsAsync(Guid taskId, CancellationToken ct = default);
    Task RemoveRelationAsync(Guid taskId, Guid relationId, CancellationToken ct = default);
    Task<TaskTagResultDto> AddTagAsync(Guid taskId, AddTaskTagRequest req, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetTagsAsync(Guid taskId, CancellationToken ct = default);
    Task RemoveTagAsync(Guid taskId, Guid tagId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskHistoryEntryDto>> GetHistoryAsync(Guid taskId, CancellationToken ct = default);
    Task<byte[]> ExportToCsvAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<TaskSavedFilterDto>> GetSavedFiltersAsync(Guid userId, CancellationToken ct = default);
    Task<TaskSavedFilterDto> CreateSavedFilterAsync(Guid userId, CreateTaskSavedFilterRequest req, CancellationToken ct = default);
    Task DeleteSavedFilterAsync(Guid filterId, Guid userId, CancellationToken ct = default);
    Task<TaskTemplateDto> CreateTemplateAsync(CreateTaskTemplateRequest req, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskTemplateDto>> ListTemplatesAsync(Guid userId, CancellationToken ct = default);
    Task<TaskTemplateDto> UpdateTemplateAsync(Guid templateId, CreateTaskTemplateRequest req, Guid userId, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid templateId, Guid userId, CancellationToken ct = default);
}
