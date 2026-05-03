using System.Text;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;
using TaskStatus = CoreBPM.Server.Domain.Tasks.TaskStatus;

namespace CoreBPM.Server.Application.Tasks.Services;

/// <summary>Реализация сервиса задач (FR-TASK-01.1, FR-TASK-01.2).</summary>
public class TaskService : ITaskService
{
    private readonly AppDbContext _db;
    private readonly IBpmNotificationService _notifications;

    /// <summary>Финальные статусы — задача не может перейти в следующий статус из этих.</summary>
    private static readonly HashSet<TaskStatus> FinalStatuses = new()
    {
        TaskStatus.Done, TaskStatus.DoneControlled,
        TaskStatus.CannotDo, TaskStatus.CannotDoControlled,
        TaskStatus.Closed,
    };

    public TaskService(AppDbContext db, IBpmNotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    /// <inheritdoc/>
    public async Task<TaskDto> CreateAsync(CreateTaskRequest req, Guid authorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Subject))
            throw new ValidationException("Тема задачи обязательна.");
        if (req.AssigneeUserId == Guid.Empty)
            throw new ValidationException("Исполнитель задачи обязателен.");

        var now = DateTimeOffset.UtcNow;
        var maxNum = await _db.TaskItems.MaxAsync(t => (int?)t.Number, ct) ?? 0;

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            Number = maxNum + 1,
            Subject = req.Subject.Trim(),
            Description = req.Description?.Trim(),
            // Если при создании указан согласующий — задача сразу уходит на предварительное согласование
            Status = req.ApproverId.HasValue ? TaskStatus.PreApproval : TaskStatus.New,
            Priority = req.Priority,
            CategoryId = req.CategoryId?.Trim(),
            AuthorUserId = authorId,
            AssigneeUserId = req.AssigneeUserId,
            StartDate = req.StartDate,
            DueDate = req.DueDate,
            DateCorrectionMode = req.DateCorrectionMode,
            PlannedEffortMinutes = req.PlannedEffortMinutes,
            ControlType = req.ControlType,
            ControllerUserId = req.ControllerUserId,
            ParentTaskId = req.ParentTaskId,
            IsOverdue = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.TaskItems.Add(task);

        foreach (var id in req.CoExecutorIds)
            _db.TaskParticipants.Add(new TaskParticipant { Id = Guid.NewGuid(), TaskId = task.Id, UserId = id, Role = TaskParticipantRole.CoExecutor, CreatedAt = now });
        foreach (var id in req.ObserverIds)
            _db.TaskParticipants.Add(new TaskParticipant { Id = Guid.NewGuid(), TaskId = task.Id, UserId = id, Role = TaskParticipantRole.Observer, CreatedAt = now });
        if (req.ApproverId.HasValue)
            _db.TaskParticipants.Add(new TaskParticipant { Id = Guid.NewGuid(), TaskId = task.Id, UserId = req.ApproverId.Value, Role = TaskParticipantRole.Approver, CreatedAt = now });
        if (req.ControllerUserId.HasValue)
            _db.TaskParticipants.Add(new TaskParticipant { Id = Guid.NewGuid(), TaskId = task.Id, UserId = req.ControllerUserId.Value, Role = TaskParticipantRole.Controller, CreatedAt = now });

        foreach (var tag in req.Tags.Where(t => !string.IsNullOrWhiteSpace(t)))
            _db.TaskTags.Add(new TaskTag { Id = Guid.NewGuid(), TaskId = task.Id, Value = tag.Trim(), CreatedAt = now });

        if (req.ReminderAt.HasValue)
            _db.TaskReminders.Add(new TaskReminder { Id = Guid.NewGuid(), TaskId = task.Id, UserId = authorId, RemindAt = req.ReminderAt.Value, IsSent = false, CreatedAt = now });

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = task.Id, ActorUserId = authorId, Action = TaskHistoryAction.Created, CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление исполнителю о новой задаче
        await _notifications.NotifyUserAsync(
            task.AssigneeUserId,
            "TaskAssigned",
            new { taskId = task.Id, number = task.Number, subject = task.Subject, dueDate = task.DueDate },
            ct);

        return await BuildDtoAsync(task.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> CreateSubtaskAsync(Guid parentTaskId, CreateTaskRequest req, Guid authorId, CancellationToken ct = default)
    {
        req.ParentTaskId = parentTaskId;
        return await CreateAsync(req, authorId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> GetAsync(Guid taskId, CancellationToken ct = default)
    {
        var exists = await _db.TaskItems.AnyAsync(t => t.Id == taskId, ct);
        if (!exists) throw new NotFoundException($"Задача {taskId} не найдена.");
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskSummaryDto>> ListAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default)
    {
        var query = _db.TaskItems.AsNoTracking();

        if (!isAdmin)
        {
            var participantTaskIds = _db.TaskParticipants
                .Where(p => p.UserId == userId)
                .Select(p => p.TaskId);
            query = query.Where(t => t.AuthorUserId == userId || t.AssigneeUserId == userId || participantTaskIds.Contains(t.Id));
        }

        if (!string.IsNullOrEmpty(filter.Status) && Enum.TryParse<TaskStatus>(filter.Status, out var status))
            query = query.Where(t => t.Status == status);
        if (!string.IsNullOrEmpty(filter.Priority) && Enum.TryParse<Domain.Tasks.TaskPriority>(filter.Priority, out var priority))
            query = query.Where(t => t.Priority == priority);
        if (filter.AssigneeId.HasValue)
            query = query.Where(t => t.AssigneeUserId == filter.AssigneeId.Value);
        if (filter.AuthorId.HasValue)
            query = query.Where(t => t.AuthorUserId == filter.AuthorId.Value);
        if (!string.IsNullOrEmpty(filter.CategoryId))
            query = query.Where(t => t.CategoryId == filter.CategoryId);
        if (filter.IsOverdue.HasValue)
            query = query.Where(t => t.IsOverdue == filter.IsOverdue.Value);
        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Поиск по номеру задачи: "T-5", "T5" или просто "5"
            if (TryExtractTaskNumber(filter.Search, out var taskNumber))
                query = query.Where(t => t.Number == taskNumber);
            else
                query = query.Where(t => t.Subject.Contains(filter.Search));
        }
        if (filter.DateFrom.HasValue)
            query = query.Where(t => t.DueDate >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(t => t.DueDate <= filter.DateTo.Value);

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var tags = await _db.TaskTags.AsNoTracking()
            .Where(t => taskIds.Contains(t.TaskId))
            .ToListAsync(ct);
        var userIds = tasks.Select(t => t.AssigneeUserId).Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        if (!string.IsNullOrEmpty(filter.TagValue))
        {
            var taggedTaskIds = tags.Where(t => t.Value == filter.TagValue).Select(t => t.TaskId).ToHashSet();
            tasks = tasks.Where(t => taggedTaskIds.Contains(t.Id)).ToList();
        }

        return tasks.Select(t => new TaskSummaryDto
        {
            Id = t.Id,
            Number = t.Number,
            Subject = t.Subject,
            Status = t.Status.ToString(),
            Priority = t.Priority.ToString(),
            CategoryId = t.CategoryId,
            AssigneeUserId = t.AssigneeUserId,
            AssigneeName = users.GetValueOrDefault(t.AssigneeUserId, t.AssigneeUserId.ToString()),
            DueDate = t.DueDate,
            IsOverdue = t.IsOverdue,
            CreatedAt = t.CreatedAt,
            Tags = tags.Where(tag => tag.TaskId == t.Id).Select(tag => tag.Value).ToList(),
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest req, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");
        var now = DateTimeOffset.UtcNow;
        var changes = new List<TaskHistoryEntry>();

        void Track(string field, string? oldVal, string? newVal)
        {
            if (oldVal != newVal)
                changes.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.Updated, FieldName = field, OldValue = oldVal, NewValue = newVal, CreatedAt = now });
        }

        if (!string.IsNullOrEmpty(req.Subject) && req.Subject != task.Subject) { Track("Subject", task.Subject, req.Subject); task.Subject = req.Subject.Trim(); }
        if (req.Description != null && req.Description != task.Description) { Track("Description", task.Description, req.Description); task.Description = req.Description.Trim(); }
        if (req.Priority.HasValue && req.Priority.Value != task.Priority) { Track("Priority", task.Priority.ToString(), req.Priority.Value.ToString()); task.Priority = req.Priority.Value; }
        if (req.CategoryId != null && req.CategoryId != task.CategoryId) { Track("CategoryId", task.CategoryId, req.CategoryId); task.CategoryId = req.CategoryId; }
        if (req.StartDate.HasValue && req.StartDate.Value != task.StartDate) { Track("StartDate", task.StartDate.ToString("O"), req.StartDate.Value.ToString("O")); task.StartDate = req.StartDate.Value; }
        if (req.DueDate.HasValue && req.DueDate.Value != task.DueDate) { Track("DueDate", task.DueDate.ToString("O"), req.DueDate.Value.ToString("O")); task.DueDate = req.DueDate.Value; }
        if (req.PlannedEffortMinutes.HasValue && req.PlannedEffortMinutes.Value != task.PlannedEffortMinutes) { Track("PlannedEffortMinutes", task.PlannedEffortMinutes?.ToString(), req.PlannedEffortMinutes.Value.ToString()); task.PlannedEffortMinutes = req.PlannedEffortMinutes.Value; }
        if (req.ControlType.HasValue && req.ControlType.Value != task.ControlType) { Track("ControlType", task.ControlType.ToString(), req.ControlType.Value.ToString()); task.ControlType = req.ControlType.Value; }
        if (req.ControllerUserId.HasValue && req.ControllerUserId.Value != task.ControllerUserId) { Track("ControllerUserId", task.ControllerUserId?.ToString(), req.ControllerUserId.Value.ToString()); task.ControllerUserId = req.ControllerUserId.Value; }

        task.UpdatedAt = now;
        _db.TaskHistoryEntries.AddRange(changes);
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");
        _db.TaskItems.Remove(task);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> CopyAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var src = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");
        var tags = await _db.TaskTags.AsNoTracking().Where(t => t.TaskId == taskId).Select(t => t.Value).ToListAsync(ct);

        var req = new CreateTaskRequest
        {
            Subject = $"Копия: {src.Subject}",
            Description = src.Description,
            Priority = src.Priority,
            CategoryId = src.CategoryId,
            AssigneeUserId = src.AssigneeUserId,
            StartDate = DateTimeOffset.UtcNow,
            DueDate = src.DueDate > DateTimeOffset.UtcNow ? src.DueDate : DateTimeOffset.UtcNow.AddDays(7),
            DateCorrectionMode = src.DateCorrectionMode,
            PlannedEffortMinutes = src.PlannedEffortMinutes,
            ControlType = src.ControlType,
            ControllerUserId = src.ControllerUserId,
            Tags = tags,
        };
        var dto = await CreateAsync(req, actorId, ct);

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = dto.Id, ActorUserId = actorId, Action = TaskHistoryAction.Copied, NewValue = taskId.ToString(), CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
        return dto;
    }

    /// <inheritdoc/>
    public async Task MarkReadAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task == null || task.Status != TaskStatus.New || task.AssigneeUserId != userId) return;
        var now = DateTimeOffset.UtcNow;
        task.Status = TaskStatus.Read;
        task.UpdatedAt = now;
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = userId, Action = TaskHistoryAction.StatusChanged, FieldName = "Status", OldValue = TaskStatus.New.ToString(), NewValue = TaskStatus.Read.ToString(), CreatedAt = now });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> ReassignAsync(Guid taskId, ReassignTaskRequest req, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");
        var now = DateTimeOffset.UtcNow;
        var oldAssignee = task.AssigneeUserId;
        task.AssigneeUserId = req.AssigneeUserId;
        task.Status = TaskStatus.New;
        task.UpdatedAt = now;
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.Reassigned, FieldName = "AssigneeUserId", OldValue = oldAssignee.ToString(), NewValue = req.AssigneeUserId.ToString(), CreatedAt = now });
        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });
        await _db.SaveChangesAsync(ct);

        // Уведомление новому исполнителю о переназначении
        await _notifications.NotifyUserAsync(
            req.AssigneeUserId,
            "TaskAssigned",
            new { taskId = task.Id, number = task.Number, subject = task.Subject, dueDate = task.DueDate },
            ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskCommentDto> AddCommentAsync(Guid taskId, AddTaskCommentRequest req, Guid authorId, CancellationToken ct = default)
    {
        if (!await _db.TaskItems.AnyAsync(t => t.Id == taskId, ct))
            throw new NotFoundException($"Задача {taskId} не найдена.");
        var now = DateTimeOffset.UtcNow;
        var comment = new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = authorId, Body = req.Body.Trim(), CreatedAt = now };
        _db.TaskComments.Add(comment);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = authorId, Action = TaskHistoryAction.CommentAdded, CreatedAt = now });
        await _db.SaveChangesAsync(ct);
        var authorName = await GetDisplayNameAsync(authorId, ct);
        return new TaskCommentDto { Id = comment.Id, AuthorUserId = authorId, AuthorName = authorName, Body = comment.Body, CreatedAt = comment.CreatedAt };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskCommentDto>> GetCommentsAsync(Guid taskId, CancellationToken ct = default)
    {
        var comments = await _db.TaskComments.AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        var userIds = comments.Select(c => c.AuthorUserId).Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return comments.Select(c => new TaskCommentDto { Id = c.Id, AuthorUserId = c.AuthorUserId, AuthorName = users.GetValueOrDefault(c.AuthorUserId, c.AuthorUserId.ToString()), Body = c.Body, CreatedAt = c.CreatedAt }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskAttachmentDto> AddAttachmentAsync(Guid taskId, AddTaskAttachmentRequest req, Guid uploadedBy, CancellationToken ct = default)
    {
        if (!await _db.TaskItems.AnyAsync(t => t.Id == taskId, ct))
            throw new NotFoundException($"Задача {taskId} не найдена.");
        var now = DateTimeOffset.UtcNow;
        var att = new TaskAttachment { Id = Guid.NewGuid(), TaskId = taskId, UploadedByUserId = uploadedBy, FileName = req.FileName, ContentType = req.ContentType, StorageKey = req.StorageKey, SizeBytes = req.SizeBytes, CreatedAt = now };
        _db.TaskAttachments.Add(att);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = uploadedBy, Action = TaskHistoryAction.AttachmentAdded, NewValue = req.FileName, CreatedAt = now });
        await _db.SaveChangesAsync(ct);
        return new TaskAttachmentDto { Id = att.Id, FileName = att.FileName, ContentType = att.ContentType, SizeBytes = att.SizeBytes, UploadedByUserId = uploadedBy, CreatedAt = att.CreatedAt };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskAttachmentDto>> GetAttachmentsAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _db.TaskAttachments.AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new TaskAttachmentDto { Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, SizeBytes = a.SizeBytes, UploadedByUserId = a.UploadedByUserId, CreatedAt = a.CreatedAt })
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TaskParticipantDto> AddParticipantAsync(Guid taskId, AddTaskParticipantRequest req, Guid actorId, CancellationToken ct = default)
    {
        if (!await _db.TaskItems.AnyAsync(t => t.Id == taskId, ct))
            throw new NotFoundException($"Задача {taskId} не найдена.");
        var existing = await _db.TaskParticipants.FirstOrDefaultAsync(p => p.TaskId == taskId && p.UserId == req.UserId && p.Role == req.Role, ct);
        if (existing != null)
        {
            var existName = await GetDisplayNameAsync(existing.UserId, ct);
            return new TaskParticipantDto { Id = existing.Id, UserId = existing.UserId, UserName = existName, Role = existing.Role.ToString() };
        }
        var now = DateTimeOffset.UtcNow;
        var participant = new TaskParticipant { Id = Guid.NewGuid(), TaskId = taskId, UserId = req.UserId, Role = req.Role, CreatedAt = now };
        _db.TaskParticipants.Add(participant);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.ParticipantAdded, NewValue = req.Role.ToString(), CreatedAt = now });
        await _db.SaveChangesAsync(ct);
        var userName = await GetDisplayNameAsync(req.UserId, ct);
        return new TaskParticipantDto { Id = participant.Id, UserId = req.UserId, UserName = userName, Role = req.Role.ToString() };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskParticipantDto>> GetParticipantsAsync(Guid taskId, CancellationToken ct = default)
    {
        var participants = await _db.TaskParticipants.AsNoTracking()
            .Where(p => p.TaskId == taskId)
            .ToListAsync(ct);
        var userIds = participants.Select(p => p.UserId).Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return participants.Select(p => new TaskParticipantDto { Id = p.Id, UserId = p.UserId, UserName = users.GetValueOrDefault(p.UserId, p.UserId.ToString()), Role = p.Role.ToString() }).ToList();
    }

    /// <inheritdoc/>
    public async Task RemoveParticipantAsync(Guid taskId, Guid participantId, CancellationToken ct = default)
    {
        var p = await _db.TaskParticipants.FirstOrDefaultAsync(x => x.Id == participantId && x.TaskId == taskId, ct);
        if (p == null) return;
        _db.TaskParticipants.Remove(p);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = p.UserId, Action = TaskHistoryAction.ParticipantRemoved, OldValue = p.Role.ToString(), CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TaskRelationDto> AddRelationAsync(Guid taskId, AddTaskRelationRequest req, Guid actorId, CancellationToken ct = default)
    {
        if (!await _db.TaskItems.AnyAsync(t => t.Id == taskId, ct))
            throw new NotFoundException($"Задача {taskId} не найдена.");
        var target = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TargetTaskId, ct)
            ?? throw new NotFoundException($"Целевая задача {req.TargetTaskId} не найдена.");
        var now = DateTimeOffset.UtcNow;
        var relation = new TaskRelation { Id = Guid.NewGuid(), SourceTaskId = taskId, TargetTaskId = req.TargetTaskId, RelationType = req.RelationType, CreatedAt = now };
        _db.TaskRelations.Add(relation);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.RelationAdded, NewValue = req.TargetTaskId.ToString(), CreatedAt = now });
        await _db.SaveChangesAsync(ct);
        return new TaskRelationDto { Id = relation.Id, SourceTaskId = taskId, TargetTaskId = req.TargetTaskId, TargetSubject = target.Subject, TargetNumber = target.Number, RelationType = req.RelationType.ToString() };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskRelationDto>> GetRelationsAsync(Guid taskId, CancellationToken ct = default)
    {
        var relations = await _db.TaskRelations.AsNoTracking()
            .Where(r => r.SourceTaskId == taskId || r.TargetTaskId == taskId)
            .ToListAsync(ct);
        var targetIds = relations.Select(r => r.TargetTaskId == taskId ? r.SourceTaskId : r.TargetTaskId).Distinct().ToList();
        var targets = await _db.TaskItems.AsNoTracking().Where(t => targetIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        return relations.Select(r =>
        {
            var otherId = r.TargetTaskId == taskId ? r.SourceTaskId : r.TargetTaskId;
            targets.TryGetValue(otherId, out var other);
            return new TaskRelationDto { Id = r.Id, SourceTaskId = r.SourceTaskId, TargetTaskId = r.TargetTaskId, TargetSubject = other?.Subject ?? string.Empty, TargetNumber = other?.Number ?? 0, RelationType = r.RelationType.ToString() };
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task RemoveRelationAsync(Guid taskId, Guid relationId, Guid actorId, CancellationToken ct = default)
    {
        var r = await _db.TaskRelations.FirstOrDefaultAsync(x => x.Id == relationId, ct);
        if (r == null) return;
        _db.TaskRelations.Remove(r);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.RelationRemoved, OldValue = r.TargetTaskId.ToString(), CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TaskTagResultDto> AddTagAsync(Guid taskId, AddTaskTagRequest req, Guid actorId, CancellationToken ct = default)
    {
        if (!await _db.TaskItems.AnyAsync(t => t.Id == taskId, ct))
            throw new NotFoundException($"Задача {taskId} не найдена.");
        var now = DateTimeOffset.UtcNow;
        var tag = new TaskTag { Id = Guid.NewGuid(), TaskId = taskId, Value = req.Value.Trim(), CreatedAt = now };
        _db.TaskTags.Add(tag);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.TagAdded, NewValue = req.Value, CreatedAt = now });
        await _db.SaveChangesAsync(ct);
        return new TaskTagResultDto { Id = tag.Id, Value = tag.Value };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetTagsAsync(Guid taskId, CancellationToken ct = default)
        => await _db.TaskTags.AsNoTracking().Where(t => t.TaskId == taskId).Select(t => t.Value).ToListAsync(ct);

    /// <inheritdoc/>
    public async Task RemoveTagAsync(Guid taskId, Guid tagId, Guid actorId, CancellationToken ct = default)
    {
        var tag = await _db.TaskTags.FirstOrDefaultAsync(t => t.Id == tagId && t.TaskId == taskId, ct);
        if (tag == null) return;
        _db.TaskTags.Remove(tag);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.TagRemoved, OldValue = tag.Value, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskHistoryEntryDto>> GetHistoryAsync(Guid taskId, CancellationToken ct = default)
    {
        var entries = await _db.TaskHistoryEntries.AsNoTracking()
            .Where(h => h.TaskId == taskId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        var userIds = entries.Where(e => e.ActorUserId != Guid.Empty).Select(e => e.ActorUserId).Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        return entries.Select(h => new TaskHistoryEntryDto { Id = h.Id, ActorUserId = h.ActorUserId, ActorName = users.GetValueOrDefault(h.ActorUserId, h.ActorUserId.ToString()), Action = h.Action.ToString(), FieldName = h.FieldName, OldValue = h.OldValue, NewValue = h.NewValue, CreatedAt = h.CreatedAt }).ToList();
    }

    /// <inheritdoc/>
    public async Task<byte[]> ExportToCsvAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default)
    {
        var tasks = await ListAsync(userId, isAdmin, filter, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Номер;Тема;Статус;Приоритет;Исполнитель;Срок;Просрочена");
        foreach (var t in tasks)
            sb.AppendLine($"T-{t.Number};\"{t.Subject}\";\"{t.Status}\";\"{t.Priority}\";\"{t.AssigneeName}\";{t.DueDate:dd.MM.yyyy HH:mm};{(t.IsOverdue ? "Да" : "Нет")}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskSavedFilterDto>> GetSavedFiltersAsync(Guid userId, CancellationToken ct = default)
        => await _db.TaskSavedFilters.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new TaskSavedFilterDto { Id = f.Id, Name = f.Name, FilterJson = f.FilterJson, CreatedAt = f.CreatedAt })
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<TaskSavedFilterDto> CreateSavedFilterAsync(Guid userId, CreateTaskSavedFilterRequest req, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var f = new TaskSavedFilter { Id = Guid.NewGuid(), UserId = userId, Name = req.Name.Trim(), FilterJson = req.FilterJson, CreatedAt = now };
        _db.TaskSavedFilters.Add(f);
        await _db.SaveChangesAsync(ct);
        return new TaskSavedFilterDto { Id = f.Id, Name = f.Name, FilterJson = f.FilterJson, CreatedAt = f.CreatedAt };
    }

    /// <inheritdoc/>
    public async Task DeleteSavedFilterAsync(Guid filterId, Guid userId, CancellationToken ct = default)
    {
        var f = await _db.TaskSavedFilters.FirstOrDefaultAsync(x => x.Id == filterId && x.UserId == userId, ct);
        if (f == null) return;
        _db.TaskSavedFilters.Remove(f);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<TaskTemplateDto> CreateTemplateAsync(CreateTaskTemplateRequest req, Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var tagsJson = System.Text.Json.JsonSerializer.Serialize(req.Tags);
        var t = new TaskTemplate { Id = Guid.NewGuid(), Name = req.Name.Trim(), DefaultAssigneeUserId = req.DefaultAssigneeUserId, DefaultPriority = req.DefaultPriority, DefaultCategoryId = req.DefaultCategoryId, Description = req.Description, ControlType = req.ControlType, PlannedEffortMinutes = req.PlannedEffortMinutes, TagsJson = tagsJson, IsPublic = req.IsPublic, CreatedByUserId = userId, CreatedAt = now, UpdatedAt = now };
        _db.TaskTemplates.Add(t);
        await _db.SaveChangesAsync(ct);
        return await BuildTemplateDtoAsync(t, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskTemplateDto>> ListTemplatesAsync(Guid userId, CancellationToken ct = default)
    {
        var templates = await _db.TaskTemplates.AsNoTracking()
            .Where(t => t.IsPublic || t.CreatedByUserId == userId)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        var result = new List<TaskTemplateDto>();
        foreach (var t in templates) result.Add(await BuildTemplateDtoAsync(t, ct));
        return result;
    }

    /// <inheritdoc/>
    public async Task<TaskTemplateDto> UpdateTemplateAsync(Guid templateId, CreateTaskTemplateRequest req, Guid userId, CancellationToken ct = default)
    {
        var t = await _db.TaskTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.CreatedByUserId == userId, ct)
            ?? throw new NotFoundException($"Шаблон {templateId} не найден.");
        t.Name = req.Name.Trim();
        t.DefaultAssigneeUserId = req.DefaultAssigneeUserId;
        t.DefaultPriority = req.DefaultPriority;
        t.DefaultCategoryId = req.DefaultCategoryId;
        t.Description = req.Description;
        t.ControlType = req.ControlType;
        t.PlannedEffortMinutes = req.PlannedEffortMinutes;
        t.TagsJson = System.Text.Json.JsonSerializer.Serialize(req.Tags);
        t.IsPublic = req.IsPublic;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await BuildTemplateDtoAsync(t, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteTemplateAsync(Guid templateId, Guid userId, CancellationToken ct = default)
    {
        var t = await _db.TaskTemplates.FirstOrDefaultAsync(x => x.Id == templateId && x.CreatedByUserId == userId, ct);
        if (t == null) return;
        _db.TaskTemplates.Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<TaskDto> BuildDtoAsync(Guid taskId, CancellationToken ct)
    {
        var task = await _db.TaskItems.AsNoTracking()
            .Include(t => t.Participants)
            .Include(t => t.Tags)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .Include(t => t.Subtasks)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var authorName = await GetDisplayNameAsync(task.AuthorUserId, ct);
        var assigneeName = await GetDisplayNameAsync(task.AssigneeUserId, ct);
        string? controllerName = task.ControllerUserId.HasValue ? await GetDisplayNameAsync(task.ControllerUserId.Value, ct) : null;

        var approverParticipant = task.Participants.FirstOrDefault(p => p.Role == TaskParticipantRole.Approver);
        string? approverName = approverParticipant != null ? await GetDisplayNameAsync(approverParticipant.UserId, ct) : null;

        var participantUserIds = task.Participants.Select(p => p.UserId).Distinct().ToList();
        var participantUsers = await _db.OrgUsers.AsNoTracking()
            .Where(u => participantUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        // Фактические трудозатраты — сумма по task_timelogs (FR-TASK-01.4)
        var actualEffort = await _db.TaskTimeLogs.AsNoTracking()
            .Where(l => l.TaskId == taskId)
            .SumAsync(l => (int?)l.DurationMinutes, ct) ?? 0;

        return new TaskDto
        {
            Id = task.Id,
            Number = task.Number,
            Subject = task.Subject,
            Description = task.Description,
            Status = task.Status.ToString(),
            Priority = task.Priority.ToString(),
            CategoryId = task.CategoryId,
            AuthorUserId = task.AuthorUserId,
            AuthorName = authorName,
            AssigneeUserId = task.AssigneeUserId,
            AssigneeName = assigneeName,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            DateCorrectionMode = task.DateCorrectionMode.ToString(),
            PlannedEffortMinutes = task.PlannedEffortMinutes,
            ActualEffortMinutes = actualEffort,
            ControlType = task.ControlType.ToString(),
            ControllerUserId = task.ControllerUserId,
            ControllerName = controllerName,
            ApproverUserId = approverParticipant?.UserId,
            ApproverName = approverName,
            ParentTaskId = task.ParentTaskId,
            IsOverdue = task.IsOverdue,
            PostponedUntil = task.PostponedUntil,
            SourceInstanceId = task.SourceInstanceId,
            SourceElementId = task.SourceElementId,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Participants = task.Participants.Select(p => new TaskParticipantDto { Id = p.Id, UserId = p.UserId, UserName = participantUsers.GetValueOrDefault(p.UserId, p.UserId.ToString()), Role = p.Role.ToString() }).ToList(),
            Tags = task.Tags.Select(t => t.Value).ToList(),
            SubtaskCount = task.Subtasks.Count,
            CommentCount = task.Comments.Count,
            AttachmentCount = task.Attachments.Count,
        };
    }

    private async Task<string> GetDisplayNameAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user?.DisplayName ?? userId.ToString();
    }

    private async Task<TaskTemplateDto> BuildTemplateDtoAsync(TaskTemplate t, CancellationToken ct)
    {
        string? assigneeName = t.DefaultAssigneeUserId.HasValue ? await GetDisplayNameAsync(t.DefaultAssigneeUserId.Value, ct) : null;
        List<string> tags = new();
        if (!string.IsNullOrEmpty(t.TagsJson))
            try { tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(t.TagsJson) ?? new(); } catch { }
        return new TaskTemplateDto { Id = t.Id, Name = t.Name, DefaultAssigneeUserId = t.DefaultAssigneeUserId, DefaultAssigneeName = assigneeName, DefaultPriority = t.DefaultPriority.ToString(), DefaultCategoryId = t.DefaultCategoryId, Description = t.Description, ControlType = t.ControlType.ToString(), PlannedEffortMinutes = t.PlannedEffortMinutes, Tags = tags, IsPublic = t.IsPublic, CreatedByUserId = t.CreatedByUserId, CreatedAt = t.CreatedAt };
    }

    // ─── FR-TASK-01.2: действия по статусам ────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskAllowedActionDto>> GetAllowedActionsAsync(
        Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.AsNoTracking()
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var status = task.Status;
        var isAssignee = task.AssigneeUserId == actorId;
        var isAuthor = task.AuthorUserId == actorId;
        var isController = task.ControllerUserId == actorId;
        var isAssigneeOrCo = IsAssigneeOrCoExecutor(task, actorId);
        var isApprover = task.Participants.Any(p => p.UserId == actorId && p.Role == TaskParticipantRole.Approver);

        var actions = new List<TaskAllowedActionDto>();

        // Предварительное согласование: согласующий или Admin могут согласовать / отказать
        if (status == TaskStatus.PreApproval && (isApprover || isAdmin))
        {
            actions.Add(new TaskAllowedActionDto { Action = "approve-pre", Label = "Согласовать" });
            actions.Add(new TaskAllowedActionDto { Action = "reject-pre", Label = "Отказать" });
        }

        // Согласование от исполнителя: исполнитель может отправить на согласование из активных статусов
        if ((status == TaskStatus.New || status == TaskStatus.Read || status == TaskStatus.InProgress
             || status == TaskStatus.ApprovalRejected || status == TaskStatus.PreApprovalRejected) && (isAssignee || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "send-for-approval", Label = "Отправить на согласование" });

        // Основное согласование: согласующий или Admin могут принять / отказать
        if (status == TaskStatus.OnApproval && (isApprover || isAdmin))
        {
            actions.Add(new TaskAllowedActionDto { Action = "approve", Label = "Согласовать" });
            actions.Add(new TaskAllowedActionDto { Action = "reject", Label = "Отказать" });
        }

        if ((status == TaskStatus.New || status == TaskStatus.Read) && (isAssigneeOrCo || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "start", Label = "Начать работу" });

        if (status == TaskStatus.InProgress && (isAssigneeOrCo || isAdmin))
        {
            actions.Add(new TaskAllowedActionDto { Action = "done", Label = "Сделано" });
            actions.Add(new TaskAllowedActionDto { Action = "cannot-do", Label = "Невозможно выполнить" });
        }

        if (!FinalStatuses.Contains(status) && (isAuthor || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "close", Label = "Закрыть" });

        if (!FinalStatuses.Contains(status) && status != TaskStatus.Postponed && (isAssignee || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "postpone", Label = "Отложить" });

        if ((status == TaskStatus.DoneNeedsControl || status == TaskStatus.CannotDoNeedsControl) && (isController || isAdmin))
        {
            actions.Add(new TaskAllowedActionDto { Action = "accept-control", Label = "Принять контроль" });
            actions.Add(new TaskAllowedActionDto { Action = "return", Label = "Вернуть на доработку" });
        }

        return actions;
    }

    /// <inheritdoc/>
    public async Task<TaskDto> StartWorkAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.New && task.Status != TaskStatus.Read)
            throw new ValidationException($"Нельзя начать работу над задачей в статусе «{task.Status}».");

        if (!IsAssigneeOrCoExecutor(task, actorId))
            throw new ValidationException("Только исполнитель или соисполнитель может начать работу по задаче.");

        // Проверка зависимостей (DependsOn): нельзя начать, пока не завершены блокирующие задачи
        var dependencyRelations = await _db.TaskRelations.AsNoTracking()
            .Where(r => r.SourceTaskId == taskId && r.RelationType == TaskRelationType.DependsOn)
            .ToListAsync(ct);

        if (dependencyRelations.Count > 0)
        {
            var dependencyIds = dependencyRelations.Select(r => r.TargetTaskId).ToList();
            var blockers = await _db.TaskItems.AsNoTracking()
                .Where(t => dependencyIds.Contains(t.Id) && !FinalStatuses.Contains(t.Status))
                .Select(t => new { t.Number, t.Subject })
                .ToListAsync(ct);

            if (blockers.Count > 0)
            {
                var list = string.Join(", ", blockers.Select(b => $"T-{b.Number} «{b.Subject}»"));
                throw new ValidationException($"Нельзя начать работу: задача зависит от незавершённых задач: {list}.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.InProgress;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.InProgress, now);
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> MarkDoneAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.InProgress)
            throw new ValidationException($"Нельзя выполнить задачу в статусе «{task.Status}».");

        if (!IsAssigneeOrCoExecutor(task, actorId))
            throw new ValidationException("Только исполнитель или соисполнитель может выполнить задачу.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        var newStatus = (task.ControlType == TaskControlType.ControlAfterExecution || task.ControlType == TaskControlType.CurrentControl)
            ? TaskStatus.DoneNeedsControl
            : TaskStatus.Done;

        task.Status = newStatus;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, newStatus, now);
        await _db.SaveChangesAsync(ct);

        // Уведомление наблюдателям при финальном статусе
        if (newStatus == TaskStatus.Done)
            await NotifyObserversAsync(task, "TaskCompleted", ct);

        // FR-TASK-01.4: «Оповещать при выполнении» — уведомить контролёра-наблюдателя
        if (task.ControlType == TaskControlType.NotifyOnCompletion && task.ControllerUserId.HasValue)
            await _notifications.NotifyUserAsync(task.ControllerUserId.Value, "TaskDoneNotification",
                new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> MarkCannotDoAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.InProgress)
            throw new ValidationException($"Нельзя отметить «Невозможно» задачу в статусе «{task.Status}».");

        if (!IsAssigneeOrCoExecutor(task, actorId))
            throw new ValidationException("Только исполнитель или соисполнитель может отметить задачу как невыполнимую.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        var newStatus = (task.ControlType == TaskControlType.ControlAfterExecution || task.ControlType == TaskControlType.CurrentControl)
            ? TaskStatus.CannotDoNeedsControl
            : TaskStatus.CannotDo;

        task.Status = newStatus;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, newStatus, now);
        await _db.SaveChangesAsync(ct);

        if (newStatus == TaskStatus.CannotDo)
            await NotifyObserversAsync(task, "TaskCompleted", ct);

        // FR-TASK-01.4: «Оповещать при выполнении» — уведомить контролёра-наблюдателя
        if (task.ControlType == TaskControlType.NotifyOnCompletion && task.ControllerUserId.HasValue)
            await _notifications.NotifyUserAsync(task.ControllerUserId.Value, "TaskDoneNotification",
                new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> CloseAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (FinalStatuses.Contains(task.Status))
            throw new ValidationException($"Нельзя закрыть задачу в финальном статусе «{task.Status}».");

        if (task.AuthorUserId != actorId && !isAdmin)
            throw new ValidationException("Только автор задачи или администратор может закрыть задачу.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.Closed;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.Closed, now);
        await _db.SaveChangesAsync(ct);

        await NotifyObserversAsync(task, "TaskCompleted", ct);
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> PostponeAsync(
        Guid taskId, PostponeTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (FinalStatuses.Contains(task.Status))
            throw new ValidationException($"Нельзя отложить задачу в финальном статусе «{task.Status}».");
        if (task.Status == TaskStatus.Postponed)
            throw new ValidationException("Задача уже отложена.");

        if (!IsAssigneeOrCoExecutor(task, actorId) && !isAdmin)
            throw new ValidationException("Только исполнитель, соисполнитель или администратор может отложить задачу.");

        if (req.PostponeUntil <= DateTimeOffset.UtcNow)
            throw new ValidationException("Дата откладывания должна быть в будущем.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.Postponed;
        task.PostponedUntil = req.PostponeUntil;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.Postponed, now);

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> AcceptControlAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.DoneNeedsControl && task.Status != TaskStatus.CannotDoNeedsControl)
            throw new ValidationException($"Принять контроль нельзя для задачи в статусе «{task.Status}».");

        if (task.ControllerUserId != actorId && !isAdmin)
            throw new ValidationException("Только контролёр или администратор может принять контроль.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        var newStatus = task.Status == TaskStatus.DoneNeedsControl
            ? TaskStatus.DoneControlled
            : TaskStatus.CannotDoControlled;

        task.Status = newStatus;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, newStatus, now);
        await _db.SaveChangesAsync(ct);

        await NotifyObserversAsync(task, "TaskCompleted", ct);
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> ReturnToWorkAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.DoneNeedsControl && task.Status != TaskStatus.CannotDoNeedsControl)
            throw new ValidationException($"Вернуть на доработку нельзя задачу в статусе «{task.Status}».");

        if (task.ControllerUserId != actorId && !isAdmin)
            throw new ValidationException("Только контролёр или администратор может вернуть задачу на доработку.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.New;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.New, now);
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(taskId, ct);
    }

    // ─── FR-TASK-01.3: Согласование ──────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskDto> ApprovePreAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.PreApproval)
            throw new ValidationException($"Нельзя согласовать задачу в статусе «{task.Status}».");

        var isApprover = task.Participants.Any(p => p.UserId == actorId && p.Role == TaskParticipantRole.Approver);
        if (!isApprover && !isAdmin)
            throw new ValidationException("Только согласующий или администратор может принять решение по согласованию.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.New;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.New, now);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.ApprovalDecisionApproved, NewValue = req.Comment?.Trim(), CreatedAt = now });

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление исполнителю
        await _notifications.NotifyUserAsync(task.AssigneeUserId, "PreApprovalApproved",
            new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> RejectPreAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.PreApproval)
            throw new ValidationException($"Нельзя отказать в согласовании задачи в статусе «{task.Status}».");

        var isApprover = task.Participants.Any(p => p.UserId == actorId && p.Role == TaskParticipantRole.Approver);
        if (!isApprover && !isAdmin)
            throw new ValidationException("Только согласующий или администратор может принять решение по согласованию.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.PreApprovalRejected;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.PreApprovalRejected, now);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.ApprovalDecisionRejected, NewValue = req.Comment?.Trim(), CreatedAt = now });

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление исполнителю
        await _notifications.NotifyUserAsync(task.AssigneeUserId, "PreApprovalRejected",
            new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> SendForApprovalAsync(Guid taskId, SendForApprovalRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var allowedStatuses = new[] { TaskStatus.New, TaskStatus.Read, TaskStatus.InProgress, TaskStatus.ApprovalRejected, TaskStatus.PreApprovalRejected };
        if (!allowedStatuses.Contains(task.Status))
            throw new ValidationException($"Нельзя отправить на согласование задачу в статусе «{task.Status}».");

        if (task.AssigneeUserId != actorId && !isAdmin)
            throw new ValidationException("Только исполнитель или администратор может отправить задачу на согласование.");

        // Если согласующий передан в запросе — добавляем его как участника (если ещё не добавлен)
        if (req.ApproverId.HasValue)
        {
            var alreadyApprover = task.Participants.Any(p => p.UserId == req.ApproverId.Value && p.Role == TaskParticipantRole.Approver);
            if (!alreadyApprover)
            {
                var now2 = DateTimeOffset.UtcNow;
                _db.TaskParticipants.Add(new TaskParticipant
                {
                    Id = Guid.NewGuid(), TaskId = taskId, UserId = req.ApproverId.Value,
                    Role = TaskParticipantRole.Approver, CreatedAt = now2
                });
                _db.TaskHistoryEntries.Add(new TaskHistoryEntry
                {
                    Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
                    Action = TaskHistoryAction.ParticipantAdded, NewValue = req.ApproverId.Value.ToString(),
                    CreatedAt = now2
                });
            }
        }
        else
        {
            // Согласующий должен уже быть назначен
            if (!task.Participants.Any(p => p.Role == TaskParticipantRole.Approver))
                throw new ValidationException("Укажите согласующего в запросе или назначьте его заранее.");
        }

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.OnApproval;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.OnApproval, now);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.SentForApproval, NewValue = req.Comment?.Trim(), CreatedAt = now });

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление согласующему
        var approverParticipant = task.Participants.FirstOrDefault(p => p.Role == TaskParticipantRole.Approver)
                                  ?? await _db.TaskParticipants.AsNoTracking()
                                      .FirstOrDefaultAsync(p => p.TaskId == taskId && p.Role == TaskParticipantRole.Approver, ct);
        if (approverParticipant != null)
            await _notifications.NotifyUserAsync(approverParticipant.UserId, "TaskSentForApproval",
                new { taskId = task.Id, number = task.Number, subject = task.Subject, dueDate = task.DueDate }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> ApproveAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.OnApproval)
            throw new ValidationException($"Нельзя согласовать задачу в статусе «{task.Status}».");

        var isApprover = task.Participants.Any(p => p.UserId == actorId && p.Role == TaskParticipantRole.Approver);
        if (!isApprover && !isAdmin)
            throw new ValidationException("Только согласующий или администратор может принять решение по согласованию.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.New;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.New, now);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.ApprovalDecisionApproved, NewValue = req.Comment?.Trim(), CreatedAt = now });

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление исполнителю
        await _notifications.NotifyUserAsync(task.AssigneeUserId, "ApprovalApproved",
            new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> RejectAsync(Guid taskId, ApprovalDecisionRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.OnApproval)
            throw new ValidationException($"Нельзя отказать в согласовании задачи в статусе «{task.Status}».");

        var isApprover = task.Participants.Any(p => p.UserId == actorId && p.Role == TaskParticipantRole.Approver);
        if (!isApprover && !isAdmin)
            throw new ValidationException("Только согласующий или администратор может принять решение по согласованию.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.ApprovalRejected;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.ApprovalRejected, now);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry { Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId, Action = TaskHistoryAction.ApprovalDecisionRejected, NewValue = req.Comment?.Trim(), CreatedAt = now });

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление исполнителю
        await _notifications.NotifyUserAsync(task.AssigneeUserId, "ApprovalRejected",
            new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskApprovalStateDto> GetApprovalStateAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.AsNoTracking()
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var approverParticipant = task.Participants.FirstOrDefault(p => p.Role == TaskParticipantRole.Approver);
        string? approverName = approverParticipant != null
            ? await GetDisplayNameAsync(approverParticipant.UserId, ct)
            : null;

        // Последнее решение — последняя запись смены статуса на Approved/Rejected/PreApprovalRejected
        var approvalStatuses = new[]
        {
            TaskStatus.Approved.ToString(), TaskStatus.PreApprovalRejected.ToString(),
            TaskStatus.ApprovalRejected.ToString(), TaskStatus.New.ToString()
        };
        var lastDecision = await _db.TaskHistoryEntries.AsNoTracking()
            .Where(h => h.TaskId == taskId
                        && h.Action == TaskHistoryAction.StatusChanged
                        && approvalStatuses.Contains(h.NewValue ?? ""))
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Комментарий согласующего — последний комментарий от него после отправки на согласование
        string? lastComment = null;
        if (approverParticipant != null && lastDecision != null)
        {
            var commentEntry = await _db.TaskComments.AsNoTracking()
                .Where(c => c.TaskId == taskId && c.AuthorUserId == approverParticipant.UserId
                            && c.CreatedAt >= lastDecision.CreatedAt.AddSeconds(-5))
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);
            lastComment = commentEntry?.Body;
        }

        return new TaskApprovalStateDto
        {
            ApproverUserId = approverParticipant?.UserId,
            ApproverName = approverName,
            Status = task.Status.ToString(),
            LastDecisionComment = lastComment,
            LastDecisionAt = lastDecision?.CreatedAt,
        };
    }

    // ─── FR-TASK-01.4: Контроль и трудозатраты ───────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskDto> UpdateControlAsync(Guid taskId, UpdateControlRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.AuthorUserId != actorId && task.ControllerUserId != actorId && !isAdmin)
            throw new ValidationException("Изменить контроль может только автор, текущий контролёр или администратор.");

        var now = DateTimeOffset.UtcNow;

        if (req.ControlType != null && Enum.TryParse<TaskControlType>(req.ControlType, true, out var ct2) && ct2 != task.ControlType)
        {
            _db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
                Action = TaskHistoryAction.Updated, FieldName = "ControlType",
                OldValue = task.ControlType.ToString(), NewValue = ct2.ToString(), CreatedAt = now,
            });
            task.ControlType = ct2;
        }

        // req.ControllerUserId: null = снять контролёра, значение = назначить
        if (req.ControllerUserId != task.ControllerUserId)
        {
            _db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
                Action = TaskHistoryAction.Updated, FieldName = "ControllerUserId",
                OldValue = task.ControllerUserId?.ToString(), NewValue = req.ControllerUserId?.ToString(), CreatedAt = now,
            });
            task.ControllerUserId = req.ControllerUserId;
        }

        task.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskTimeLogDto> AddTimeLogAsync(Guid taskId, AddTimeLogRequest req, Guid userId, CancellationToken ct = default)
    {
        _ = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (req.DurationMinutes <= 0)
            throw new ValidationException("Длительность должна быть больше 0 минут.");

        var now = DateTimeOffset.UtcNow;
        var log = new TaskTimeLog
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            ActivityTypeId = req.ActivityTypeId,
            DurationMinutes = req.DurationMinutes,
            StartDate = req.StartDate == default ? now : req.StartDate,
            Comment = req.Comment?.Trim(),
            CreatedAt = now,
        };
        _db.TaskTimeLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        var userName = await GetDisplayNameAsync(userId, ct);
        string? activityTypeName = null;
        if (req.ActivityTypeId.HasValue)
            activityTypeName = (await _db.TaskActivityTypes.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == req.ActivityTypeId.Value, ct))?.Name;

        return new TaskTimeLogDto
        {
            Id = log.Id, TaskId = taskId, UserId = userId, UserName = userName,
            ActivityTypeId = req.ActivityTypeId, ActivityTypeName = activityTypeName,
            DurationMinutes = req.DurationMinutes, StartDate = log.StartDate,
            Comment = log.Comment, CreatedAt = log.CreatedAt,
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskTimeLogDto>> GetTimeLogsAsync(Guid taskId, CancellationToken ct = default)
    {
        _ = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var logs = await _db.TaskTimeLogs.AsNoTracking()
            .Where(l => l.TaskId == taskId)
            .OrderBy(l => l.StartDate)
            .ToListAsync(ct);

        var userIds = logs.Select(l => l.UserId).Distinct().ToList();
        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var activityIds = logs.Where(l => l.ActivityTypeId.HasValue).Select(l => l.ActivityTypeId!.Value).Distinct().ToList();
        var activityNames = activityIds.Count > 0
            ? await _db.TaskActivityTypes.AsNoTracking()
                .Where(a => activityIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.Name, ct)
            : new Dictionary<Guid, string>();

        return logs.Select(l => new TaskTimeLogDto
        {
            Id = l.Id, TaskId = l.TaskId, UserId = l.UserId,
            UserName = userNames.GetValueOrDefault(l.UserId, l.UserId.ToString()),
            ActivityTypeId = l.ActivityTypeId,
            ActivityTypeName = l.ActivityTypeId.HasValue ? activityNames.GetValueOrDefault(l.ActivityTypeId.Value) : null,
            DurationMinutes = l.DurationMinutes, StartDate = l.StartDate,
            Comment = l.Comment, CreatedAt = l.CreatedAt,
        }).ToList();
    }

    private void AddStatusHistory(Guid taskId, Guid actorId, TaskStatus from, TaskStatus to, DateTimeOffset now)
    {
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.StatusChanged,
            FieldName = "Status",
            OldValue = from.ToString(),
            NewValue = to.ToString(),
            CreatedAt = now,
        });
    }

    private async Task NotifyObserversAsync(TaskItem task, string eventType, CancellationToken ct)
    {
        var observerIds = task.Participants
            .Where(p => p.Role == TaskParticipantRole.Observer)
            .Select(p => p.UserId)
            .ToList();

        var payload = new { taskId = task.Id, subject = task.Subject, status = task.Status.ToString() };
        foreach (var userId in observerIds)
            await _notifications.NotifyUserAsync(userId, eventType, payload, ct);
    }

    /// <summary>Проверяет, является ли актор исполнителем или соисполнителем задачи.</summary>
    private static bool IsAssigneeOrCoExecutor(TaskItem task, Guid actorId)
        => task.AssigneeUserId == actorId
           || task.Participants.Any(p => p.UserId == actorId && p.Role == TaskParticipantRole.CoExecutor);

    /// <summary>
    /// Пытается извлечь номер задачи из строки поиска.
    /// Поддерживаемые форматы: «T-5», «T5» и просто «5».
    /// Возвращает <c>true</c> и значение <paramref name="number"/>, если строка является номером задачи.
    /// </summary>
    private static bool TryExtractTaskNumber(string search, out int number)
    {
        var trimmed = search.Trim();

        string candidate;
        if (trimmed.StartsWith("T-", StringComparison.OrdinalIgnoreCase))
            candidate = trimmed[2..];
        else if (trimmed.StartsWith("T", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 1)
            candidate = trimmed[1..];
        else
            candidate = trimmed;

        return int.TryParse(candidate, out number);
    }
}
