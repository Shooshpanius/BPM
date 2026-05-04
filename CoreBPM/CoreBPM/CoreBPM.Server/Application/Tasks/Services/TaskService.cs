using System.Text;
using System.IO;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;
using CoreBPM.Server.Domain.Bpm;
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
    private readonly ITaskControlSettingsService _controlSettings;

    /// <summary>Финальные статусы — задача не может перейти в следующий статус из этих.</summary>
    private static readonly HashSet<TaskStatus> FinalStatuses = new()
    {
        TaskStatus.Done, TaskStatus.DoneControlled,
        TaskStatus.CannotDo, TaskStatus.CannotDoControlled,
        TaskStatus.Closed,
    };

    /// <summary>Регулярное выражение для поиска @упоминаний в тексте комментария.</summary>
    private static readonly System.Text.RegularExpressions.Regex MentionRegex =
        new(@"@([\w\.\-]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    public TaskService(AppDbContext db, IBpmNotificationService notifications, ITaskControlSettingsService controlSettings)
    {
        _db = db;
        _notifications = notifications;
        _controlSettings = controlSettings;
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
            Kind = req.Kind,
            DocumentId = req.DocumentId,
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

        // ─── Группа задач (FR-TASK-02.2) ─────────────────────────────────
        switch (filter.Group)
        {
            case "incoming":
                query = query.Where(t => t.AssigneeUserId == userId);
                break;
            case "outgoing":
                query = query.Where(t => t.AuthorUserId == userId);
                break;
            case "control":
                query = query.Where(t => t.ControllerUserId == userId);
                break;
            case "co-exec":
                var coExecTaskIds = _db.TaskParticipants
                    .Where(p => p.UserId == userId && p.Role == TaskParticipantRole.CoExecutor)
                    .Select(p => p.TaskId);
                query = query.Where(t => coExecTaskIds.Contains(t.Id));
                break;
            default:
                if (!isAdmin)
                {
                    var participantTaskIds = _db.TaskParticipants
                        .Where(p => p.UserId == userId)
                        .Select(p => p.TaskId);
                    query = query.Where(t => t.AuthorUserId == userId || t.AssigneeUserId == userId || participantTaskIds.Contains(t.Id));
                }
                break;
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
        // Фильтр по родительской задаче (для загрузки подзадач)
        if (filter.ParentTaskId.HasValue)
            query = query.Where(t => t.ParentTaskId == filter.ParentTaskId.Value);

        // ─── Сортировка (FR-TASK-02.2) ────────────────────────────────────
        query = (filter.SortBy, filter.SortDir?.ToLowerInvariant()) switch
        {
            ("due_date",  "asc")  => query.OrderBy(t => t.DueDate),
            ("due_date",  _)      => query.OrderByDescending(t => t.DueDate),
            ("priority",  "asc")  => query.OrderBy(t => t.Priority),
            ("priority",  _)      => query.OrderByDescending(t => t.Priority),
            ("status",    "asc")  => query.OrderBy(t => t.Status),
            ("status",    _)      => query.OrderByDescending(t => t.Status),
            ("subject",   "asc")  => query.OrderBy(t => t.Subject),
            ("subject",   _)      => query.OrderByDescending(t => t.Subject),
            ("created_at","asc")  => query.OrderBy(t => t.CreatedAt),
            _                     => query.OrderByDescending(t => t.CreatedAt),
        };

        // ─── Пагинация (FR-TASK-02.2) ─────────────────────────────────────
        var pageSize = filter.PageSize is > 0 and <= 200 ? filter.PageSize : 50;
        var page = filter.Page > 0 ? filter.Page : 1;
        query = query.Skip((page - 1) * pageSize).Take(pageSize);

        var tasks = await query.ToListAsync(ct);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var tags = await _db.TaskTags.AsNoTracking()
            .Where(t => taskIds.Contains(t.TaskId))
            .ToListAsync(ct);

        var participants = await _db.TaskParticipants.AsNoTracking()
            .Where(p => taskIds.Contains(p.TaskId))
            .ToListAsync(ct);

        var openQuestionsCount = await _db.TaskQuestions.AsNoTracking()
            .Where(q => taskIds.Contains(q.TaskId) && q.AnsweredAt == null)
            .GroupBy(q => q.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count, ct);

        var userIds = tasks.Select(t => t.AssigneeUserId)
            .Concat(tasks.Select(t => t.AuthorUserId))
            .Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        if (!string.IsNullOrEmpty(filter.TagValue))
        {
            var taggedTaskIds = tags.Where(t => t.Value == filter.TagValue).Select(t => t.TaskId).ToHashSet();
            tasks = tasks.Where(t => taggedTaskIds.Contains(t.Id)).ToList();
        }

        var coExecSet = participants
            .Where(p => p.UserId == userId && p.Role == TaskParticipantRole.CoExecutor)
            .Select(p => p.TaskId)
            .ToHashSet();

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
            AuthorUserId = t.AuthorUserId,
            AuthorName = users.GetValueOrDefault(t.AuthorUserId, t.AuthorUserId.ToString()),
            DueDate = t.DueDate,
            IsOverdue = t.IsOverdue,
            CreatedAt = t.CreatedAt,
            Tags = tags.Where(tag => tag.TaskId == t.Id).Select(tag => tag.Value).ToList(),
            Kind = t.Kind.ToString(),
            ScheduledAt = t.ScheduledAt,
            IsCoExecutor = coExecSet.Contains(t.Id),
            OpenQuestionCount = openQuestionsCount.GetValueOrDefault(t.Id, 0),
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskDto> UpdateAsync(Guid taskId, UpdateTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        // Редактирование доступно только автору задачи или Admin
        if (task.AuthorUserId != actorId && !isAdmin)
            throw new ForbiddenException("Редактировать задачу может только её автор или администратор.");
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

        if (src.Kind == TaskKind.Resolution)
            throw new ValidationException("Задачи по резолюции документа копировать запрещено.");

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

        // FR-TASK-02.3: Уведомление наблюдателей о переназначении
        await NotifyObserversAsync(task, "TaskReassigned", ct);

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

        // FR-TASK-02.1: Обработка упоминаний @displayname в тексте комментария
        await ProcessMentionsAsync(taskId, req.Body, authorId, now, ct);

        // FR-TASK-02.3: Уведомление наблюдателей о новом комментарии
        var taskForNotify = await _db.TaskItems.Include(t => t.Participants)
            .AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (taskForNotify != null)
            await NotifyObserversAsync(taskForNotify, "TaskCommentAdded", ct);

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

        // Суммирование трудозатрат по подзадачам (FR-TASK-01.4)
        var subtaskIds = task.Subtasks.Select(s => s.Id).ToList();
        var subtaskEffort = subtaskIds.Any()
            ? await _db.TaskTimeLogs.AsNoTracking()
                .Where(l => subtaskIds.Contains(l.TaskId))
                .SumAsync(l => (int?)l.DurationMinutes, ct) ?? 0
            : 0;

        var dto = new TaskDto
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
            SubtaskActualEffortMinutes = subtaskEffort,
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
            Kind = task.Kind.ToString(),
            DocumentId = task.DocumentId,
            SeriesId = task.SeriesId,
            ScheduledAt = task.ScheduledAt,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            Participants = task.Participants.Select(p => new TaskParticipantDto { Id = p.Id, UserId = p.UserId, UserName = participantUsers.GetValueOrDefault(p.UserId, p.UserId.ToString()), Role = p.Role.ToString() }).ToList(),
            Tags = task.Tags.Select(t => t.Value).ToList(),
            SubtaskCount = task.Subtasks.Count,
            CommentCount = task.Comments.Count,
            AttachmentCount = task.Attachments.Count,
        };

        // Заполняем детали процесса (FR-TASK-01.5.2)
        if (task.Kind == TaskKind.ProcessTask && task.SourceInstanceId.HasValue)
            dto.ProcessInfo = await GetProcessTaskInfoAsync(task.Id, ct);

        // Заполняем конфигурацию серии (FR-TASK-01.5.1)
        if (task.Kind == TaskKind.Periodic)
        {
            var recId = task.SeriesId ?? task.Id;
            var rec = await _db.TaskRecurrences.AsNoTracking().FirstOrDefaultAsync(r => r.RootTaskId == task.Id || r.Id == recId, ct);
            if (rec != null) dto.Recurrence = MapRecurrenceDto(rec);
        }

        return dto;
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

        // FR-TASK-01.4: взять / снять задачу с текущего контроля
        if (!FinalStatuses.Contains(status) && task.ControllerUserId != actorId)
            actions.Add(new TaskAllowedActionDto { Action = "take-control", Label = "Взять на контроль" });

        if (!FinalStatuses.Contains(status) && (isController || isAdmin) && task.ControllerUserId.HasValue)
            actions.Add(new TaskAllowedActionDto { Action = "release-control", Label = "Снять с контроля" });

        // FR-TASK-02.1: дополнительные действия

        // Перенести срок: автор, контролёр, Admin
        if (!FinalStatuses.Contains(status) && (isAuthor || isController || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "reschedule", Label = "Перенести срок" });

        // Открыть заново: из Done/DoneControlled/CannotDo/CannotDoControlled — автор, контролёр, Admin
        var reopenableStatuses = new HashSet<TaskStatus>
        {
            TaskStatus.Done, TaskStatus.DoneControlled, TaskStatus.CannotDo, TaskStatus.CannotDoControlled,
        };
        if (reopenableStatuses.Contains(status) && (isAuthor || isController || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "reopen", Label = "Открыть заново" });

        // Взять задачу из очереди: любой, кто ещё не является исполнителем
        if (!FinalStatuses.Contains(status) && !isAssignee)
            actions.Add(new TaskAllowedActionDto { Action = "claim", Label = "Взять задачу" });

        // Редактирование задачи: автор или Admin; не применимо к закрытым задачам
        if (!FinalStatuses.Contains(status) && (isAuthor || isAdmin))
            actions.Add(new TaskAllowedActionDto { Action = "edit", Label = "Изменить" });

        return actions;
    }

    /// <inheritdoc/>
    public async Task<TaskDto> StartWorkAsync(Guid taskId, StartWorkRequest? req, Guid actorId, CancellationToken ct = default)
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

        if (!string.IsNullOrWhiteSpace(req?.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // Уведомление соисполнителей о начале работы
        if (req?.NotifyCoExecutors == true)
            await NotifyCoExecutorsAsync(task, "TaskStarted", ct);

        // FR-TASK-02.3: Уведомление наблюдателей о начале работы
        await NotifyObserversAsync(task, "TaskStarted", ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> MarkDoneAsync(Guid taskId, MarkDoneRequest? req, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.Status != TaskStatus.InProgress)
            throw new ValidationException($"Нельзя выполнить задачу в статусе «{task.Status}».");

        if (!IsAssigneeOrCoExecutor(task, actorId))
            throw new ValidationException("Только исполнитель или соисполнитель может выполнить задачу.");

        // FR-TASK-01.4: проверка обязательности трудозатрат
        var ctrlSettings = await _controlSettings.GetAsync(ct);
        if (ctrlSettings.IsEffortRequired)
        {
            var hasLogs = await _db.TaskTimeLogs.AsNoTracking()
                .AnyAsync(l => l.TaskId == taskId, ct);
            // Разрешаем, если трудозатраты переданы прямо в запросе
            if (!hasLogs && (req?.EffortMinutes == null || req.EffortMinutes <= 0))
                throw new ValidationException("Перед выполнением задачи необходимо добавить трудозатраты.");
        }

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        var newStatus = (task.ControlType == TaskControlType.ControlAfterExecution || task.ControlType == TaskControlType.CurrentControl)
            ? TaskStatus.DoneNeedsControl
            : TaskStatus.Done;

        task.Status = newStatus;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, newStatus, now);

        if (!string.IsNullOrWhiteSpace(req?.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        // Сохранить трудозатраты из запроса
        if (req?.EffortMinutes > 0)
            _db.TaskTimeLogs.Add(new TaskTimeLog
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                UserId = actorId,
                StartDate = req.WorkStartDate ?? now,
                DurationMinutes = req.EffortMinutes.Value,
                CreatedAt = now,
            });

        // Скопировать вложения из подзадач
        if (req?.CopyAttachmentsFromSubtasks == true)
        {
            var subtaskIds = await _db.TaskItems.AsNoTracking()
                .Where(t => t.ParentTaskId == taskId)
                .Select(t => t.Id)
                .ToListAsync(ct);
            if (subtaskIds.Count > 0)
            {
                var subAttachments = await _db.TaskAttachments.AsNoTracking()
                    .Where(a => subtaskIds.Contains(a.TaskId))
                    .ToListAsync(ct);
                foreach (var att in subAttachments)
                    _db.TaskAttachments.Add(new TaskAttachment
                    {
                        Id = Guid.NewGuid(),
                        TaskId = taskId,
                        FileName = att.FileName,
                        ContentType = att.ContentType,
                        StorageKey = att.StorageKey,
                        SizeBytes = att.SizeBytes,
                        UploadedByUserId = actorId,
                        CreatedAt = now,
                    });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Уведомление соисполнителям
        if (req?.NotifyCoExecutors == true)
            await NotifyCoExecutorsAsync(task, "TaskCompleted", ct);

        // Уведомление наблюдателям при финальном статусе
        if (newStatus == TaskStatus.Done)
            await NotifyObserversAsync(task, "TaskCompleted", ct);

        // FR-TASK-01.4: «Оповещать при выполнении» — уведомить контролёра-наблюдателя
        if (task.ControlType == TaskControlType.NotifyOnCompletion && task.ControllerUserId.HasValue)
            await _notifications.NotifyUserAsync(task.ControllerUserId.Value, "TaskDoneNotification",
                new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        // FR-TASK-01.5.3: уведомить автора резолюции при выполнении задачи по резолюции документа
        if (task.Kind == TaskKind.Resolution && task.AuthorUserId != actorId)
            await _notifications.NotifyUserAsync(task.AuthorUserId, "ResolutionTaskDone",
                new { taskId = task.Id, number = task.Number, subject = task.Subject, documentId = task.DocumentId }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> MarkCannotDoAsync(Guid taskId, MarkCannotDoRequest? req, Guid actorId, CancellationToken ct = default)
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

        if (!string.IsNullOrWhiteSpace(req?.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        if (newStatus == TaskStatus.CannotDo)
            await NotifyObserversAsync(task, "TaskCompleted", ct);

        if (req?.NotifyCoExecutors == true)
            await NotifyCoExecutorsAsync(task, "TaskCompleted", ct);

        // FR-TASK-01.4: «Оповещать при выполнении» — уведомить контролёра-наблюдателя
        if (task.ControlType == TaskControlType.NotifyOnCompletion && task.ControllerUserId.HasValue)
            await _notifications.NotifyUserAsync(task.ControllerUserId.Value, "TaskDoneNotification",
                new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> CloseAsync(Guid taskId, CloseTaskRequest? req, Guid actorId, bool isAdmin, CancellationToken ct = default)
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

        if (!string.IsNullOrWhiteSpace(req?.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        await NotifyObserversAsync(task, "TaskCompleted", ct);
        if (req?.NotifyCoExecutors == true)
            await NotifyCoExecutorsAsync(task, "TaskCompleted", ct);

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
                var approverAddedAt = DateTimeOffset.UtcNow;
                _db.TaskParticipants.Add(new TaskParticipant
                {
                    Id = Guid.NewGuid(), TaskId = taskId, UserId = req.ApproverId.Value,
                    Role = TaskParticipantRole.Approver, CreatedAt = approverAddedAt
                });
                _db.TaskHistoryEntries.Add(new TaskHistoryEntry
                {
                    Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
                    Action = TaskHistoryAction.ParticipantAdded, NewValue = req.ApproverId.Value.ToString(),
                    CreatedAt = approverAddedAt
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

        if (req.ControlType != null && Enum.TryParse<TaskControlType>(req.ControlType, true, out var parsedControlType) && parsedControlType != task.ControlType)
        {
            _db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
                Action = TaskHistoryAction.Updated, FieldName = "ControlType",
                OldValue = task.ControlType.ToString(), NewValue = parsedControlType.ToString(), CreatedAt = now,
            });
            task.ControlType = parsedControlType;
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

        // FR-TASK-01.4: проверка обязательности вида деятельности
        var ctrlSettings = await _controlSettings.GetAsync(ct);
        if (ctrlSettings.IsActivityTypeRequired && req.ActivityTypeId is null)
            throw new ValidationException("Вид деятельности является обязательным полем при добавлении трудозатрат.");

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

    /// <summary>
    /// Обрабатывает упоминания @ИмяПользователя в тексте комментария (FR-TASK-02.1).
    /// Упомянутые пользователи получают уведомление и автоматически добавляются в наблюдатели.
    /// </summary>
    private async Task ProcessMentionsAsync(Guid taskId, string text, Guid authorId, DateTimeOffset now, CancellationToken ct)
    {
        // Извлекаем уникальные упоминания @слово
        var matches = MentionRegex.Matches(text);
        if (matches.Count == 0) return;

        var mentionedNames = matches
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();

        // Ищем пользователей по DisplayName или WorkEmail (по нормализованному имени)
        var allUsers = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.DisplayName, WorkEmail = u.WorkEmail })
            .ToListAsync(ct);

        // Нормализуем данные пользователей заранее для эффективного сравнения
        var normalizedUsers = allUsers.Select(u => new
        {
            u.Id,
            NormalizedName = u.DisplayName.Replace(" ", "").ToLowerInvariant(),
            NormalizedEmail = (!string.IsNullOrEmpty(u.WorkEmail) && u.WorkEmail.Contains('@'))
                    ? u.WorkEmail.Split('@')[0].ToLowerInvariant()
                    : (u.WorkEmail?.ToLowerInvariant() ?? string.Empty),
        }).ToList();

        var mentionedUserIds = normalizedUsers
            .Where(u => mentionedNames.Any(m => u.NormalizedName.Contains(m) || u.NormalizedEmail == m))
            .Select(u => u.Id)
            .Where(id => id != authorId)
            .Distinct()
            .ToList();

        if (mentionedUserIds.Count == 0) return;

        var existingWatchers = await _db.TaskParticipants.AsNoTracking()
            .Where(p => p.TaskId == taskId && p.Role == TaskParticipantRole.Observer && mentionedUserIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .ToListAsync(ct);

        bool hasNewWatchers = false;
        foreach (var userId in mentionedUserIds)
        {
            if (!existingWatchers.Contains(userId))
            {
                _db.TaskParticipants.Add(new TaskParticipant { Id = Guid.NewGuid(), TaskId = taskId, UserId = userId, Role = TaskParticipantRole.Observer, CreatedAt = now });
                hasNewWatchers = true;
            }

            _db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                ActorUserId = authorId,
                Action = TaskHistoryAction.Mentioned,
                NewValue = userId.ToString(),
                CreatedAt = now,
            });
        }

        if (hasNewWatchers) await _db.SaveChangesAsync(ct);

        // Уведомить упомянутых пользователей
        var task = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        foreach (var userId in mentionedUserIds)
            await _notifications.NotifyUserAsync(userId, "TaskMentioned",
                new { taskId, number = task?.Number, subject = task?.Subject }, ct);
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

    // ─── FR-TASK-01.4: Взять / снять контроль, удалить трудозатраты ──────────

    /// <inheritdoc/>
    public async Task<TaskDto> TakeControlAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (FinalStatuses.Contains(task.Status))
            throw new ValidationException("Нельзя взять на контроль задачу в финальном статусе.");

        var now = DateTimeOffset.UtcNow;

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
            Action = TaskHistoryAction.Updated, FieldName = "ControllerUserId",
            OldValue = task.ControllerUserId?.ToString(),
            NewValue = actorId.ToString(), CreatedAt = now,
        });

        task.ControllerUserId = actorId;
        task.ControlType = TaskControlType.CurrentControl;
        task.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> ReleaseControlAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.ControllerUserId != actorId && !isAdmin)
            throw new ValidationException("Снять контроль может только текущий контролёр или администратор.");

        var now = DateTimeOffset.UtcNow;

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
            Action = TaskHistoryAction.Updated, FieldName = "ControllerUserId",
            OldValue = task.ControllerUserId?.ToString(),
            NewValue = null, CreatedAt = now,
        });

        task.ControllerUserId = null;
        task.ControlType = TaskControlType.None;
        task.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteTimeLogAsync(Guid taskId, Guid logId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        _ = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var log = await _db.TaskTimeLogs
            .FirstOrDefaultAsync(l => l.Id == logId && l.TaskId == taskId, ct)
            ?? throw new NotFoundException($"Запись трудозатрат {logId} не найдена.");

        if (log.UserId != actorId && !isAdmin)
            throw new ValidationException("Удалить трудозатраты может только их автор или администратор.");

        _db.TaskTimeLogs.Remove(log);

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(), TaskId = taskId, ActorUserId = actorId,
            Action = TaskHistoryAction.Updated, FieldName = "TimeLog",
            OldValue = $"{log.DurationMinutes} мин ({log.StartDate:d})", NewValue = null,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }

    // ─── FR-TASK-01.5: Типы задач ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskDto> CreatePeriodicTaskAsync(CreatePeriodicTaskRequest req, Guid authorId, CancellationToken ct = default)
    {
        req.Kind = TaskKind.Periodic;
        var dto = await CreateAsync(req, authorId, ct);

        var now = DateTimeOffset.UtcNow;
        var recurrence = new TaskRecurrence
        {
            Id = Guid.NewGuid(),
            RootTaskId = dto.Id,
            Periodicity = req.Periodicity,
            EndCondition = req.EndCondition,
            EndDate = req.EndDate,
            LookAheadCount = req.LookAheadCount,
            DurationMinutes = req.DurationMinutes,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.TaskRecurrences.Add(recurrence);
        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(dto.Id, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskRecurrenceDto> UpdateSeriesAsync(Guid rootTaskId, UpdateSeriesRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var root = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == rootTaskId, ct)
            ?? throw new NotFoundException($"Задача {rootTaskId} не найдена.");
        if (root.AuthorUserId != actorId && !isAdmin)
            throw new ValidationException("Редактировать серию может только автор или администратор.");

        var rec = await _db.TaskRecurrences.FirstOrDefaultAsync(r => r.RootTaskId == rootTaskId, ct)
            ?? throw new NotFoundException($"Серия для задачи {rootTaskId} не найдена.");

        if (req.Periodicity.HasValue) rec.Periodicity = req.Periodicity.Value;
        if (req.EndCondition.HasValue) rec.EndCondition = req.EndCondition.Value;
        if (req.EndDate.HasValue) rec.EndDate = req.EndDate;
        if (req.LookAheadCount.HasValue) rec.LookAheadCount = req.LookAheadCount.Value;
        if (req.DurationMinutes.HasValue) rec.DurationMinutes = req.DurationMinutes.Value;
        rec.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapRecurrenceDto(rec);
    }

    /// <inheritdoc/>
    public async Task StopSeriesAsync(Guid rootTaskId, Guid actorId, bool isAdmin, string? activeTaskAction = null, CancellationToken ct = default)
    {
        var root = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == rootTaskId, ct)
            ?? throw new NotFoundException($"Задача {rootTaskId} не найдена.");
        if (root.AuthorUserId != actorId && !isAdmin)
            throw new ValidationException("Остановить серию может только автор или администратор.");

        var rec = await _db.TaskRecurrences.FirstOrDefaultAsync(r => r.RootTaskId == rootTaskId, ct)
            ?? throw new NotFoundException($"Серия для задачи {rootTaskId} не найдена.");
        rec.IsActive = false;
        rec.UpdatedAt = DateTimeOffset.UtcNow;

        // FR-TASK-01.5.1: обработка активных экземпляров серии
        if (!string.IsNullOrEmpty(activeTaskAction))
        {
            var activeItems = await _db.TaskItems
                .Where(t =>
                    (t.SeriesId == rec.Id || t.Id == rootTaskId)
                    && t.Status != Domain.Tasks.TaskStatus.Done
                    && t.Status != Domain.Tasks.TaskStatus.Closed
                    && t.Status != Domain.Tasks.TaskStatus.CannotDo)
                .ToListAsync(ct);

            var now = DateTimeOffset.UtcNow;
            if (activeTaskAction == "ForceComplete")
            {
                foreach (var item in activeItems)
                {
                    var oldStatus = item.Status;
                    item.Status = Domain.Tasks.TaskStatus.Done;
                    item.UpdatedAt = now;
                    _db.TaskHistoryEntries.Add(new TaskHistoryEntry
                    {
                        Id = Guid.NewGuid(), TaskId = item.Id, ActorUserId = actorId,
                        Action = TaskHistoryAction.StatusChanged,
                        OldValue = oldStatus.ToString(), NewValue = Domain.Tasks.TaskStatus.Done.ToString(),
                        CreatedAt = now,
                    });
                }
            }
            else if (activeTaskAction == "Delete")
            {
                _db.TaskItems.RemoveRange(activeItems);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PeriodicSeriesItemDto>> GetSeriesItemsAsync(Guid rootTaskId, bool activeOnly, CancellationToken ct = default)
    {
        var root = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == rootTaskId, ct)
            ?? throw new NotFoundException($"Задача {rootTaskId} не найдена.");

        var query = _db.TaskItems.AsNoTracking()
            .Where(t => t.SeriesId == root.SeriesId || t.Id == rootTaskId);

        if (activeOnly)
            query = query.Where(t => t.Status != TaskStatus.Done && t.Status != TaskStatus.Closed && t.Status != TaskStatus.CannotDo);

        var items = await query.OrderBy(t => t.StartDate).ToListAsync(ct);
        return items.Select(t => new PeriodicSeriesItemDto
        {
            Id = t.Id,
            Number = t.Number,
            Subject = t.Subject,
            Status = t.Status.ToString(),
            StartDate = t.StartDate,
            DueDate = t.DueDate,
            IsOverdue = t.IsOverdue,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskDto> CreateResolutionTaskAsync(CreateResolutionTaskRequest req, Guid authorId, CancellationToken ct = default)
    {
        req.Kind = TaskKind.Resolution;
        // DocumentId установлен непосредственно в CreateResolutionTaskRequest; передаётся в CreateAsync через базовый DocumentId
        return await CreateAsync(req, authorId, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskDto>> GetDocumentResolutionsAsync(Guid documentId, CancellationToken ct = default)
    {
        var ids = await _db.TaskItems.AsNoTracking()
            .Where(t => t.DocumentId == documentId && t.Kind == TaskKind.Resolution)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var result = new List<TaskDto>(ids.Count);
        foreach (var id in ids)
            result.Add(await BuildDtoAsync(id, ct));
        return result;
    }

    /// <inheritdoc/>
    public async Task<ProcessTaskInfoDto?> GetProcessTaskInfoAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (task.SourceInstanceId == null) return null;

        var instance = await _db.BpmInstances.AsNoTracking()
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .FirstOrDefaultAsync(i => i.Id == task.SourceInstanceId.Value, ct);

        if (instance == null) return null;

        var initiatorName = instance.InitiatorUserId.HasValue
            ? await GetDisplayNameAsync(instance.InitiatorUserId.Value, ct)
            : string.Empty;
        string? ownerName = instance.ResponsibleUserId.HasValue
            ? await GetDisplayNameAsync(instance.ResponsibleUserId.Value, ct)
            : null;

        return new ProcessTaskInfoDto
        {
            InstanceId = instance.Id,
            InstanceTitle = instance.Name,
            ProcessName = instance.Process.Name,
            ProcessVersionNumber = $"v{instance.ProcessVersion.VersionNumber}",
            LaunchedAt = instance.StartedAt,
            InitiatorUserId = instance.InitiatorUserId ?? Guid.Empty,
            InitiatorName = initiatorName,
            OwnerUserId = instance.ResponsibleUserId,
            OwnerName = ownerName,
        };
    }

    /// <inheritdoc/>
    public async Task<(Stream ZipStream, string FileName)> DownloadAttachmentsZipAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var attachments = await _db.TaskAttachments.AsNoTracking()
            .Where(a => a.TaskId == taskId)
            .ToListAsync(ct);

        var memStream = new System.IO.MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var att in attachments)
            {
                var entry = zip.CreateEntry(att.FileName, System.IO.Compression.CompressionLevel.Fastest);
                // Записываем placeholder — реальный S3/FileStorage вызов заменить здесь
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"[{att.StorageKey}]"), ct);
            }
        }
        memStream.Position = 0;
        return (memStream, $"T-{task.Number}-attachments.zip");
    }

    /// <inheritdoc/>
    public async Task<TaskItem?> CreateNextPeriodicInstanceAsync(Guid recurrenceId, CancellationToken ct = default)
    {
        var rec = await _db.TaskRecurrences.AsNoTracking()
            .Include(r => r.RootTask)
            .FirstOrDefaultAsync(r => r.Id == recurrenceId, ct);
        if (rec == null || !rec.IsActive) return null;

        // Проверяем условие завершения серии
        if (rec.EndCondition == TaskSeriesEndCondition.ByDate && rec.EndDate.HasValue && DateTimeOffset.UtcNow >= rec.EndDate.Value)
            return null;

        // FR-TASK-01.5.1: не создавать экземпляр, если исполнитель заблокирован (IsActive = false)
        if (rec.RootTask.AssigneeUserId != Guid.Empty)
        {
            var assignee = await _db.OrgUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == rec.RootTask.AssigneeUserId, ct);
            if (assignee != null && !assignee.IsActive)
                return null;
        }

        // Находим последний экземпляр серии
        var lastInstance = await _db.TaskItems.AsNoTracking()
            .Where(t => t.SeriesId == recurrenceId || t.Id == rec.RootTaskId)
            .OrderByDescending(t => t.StartDate)
            .FirstOrDefaultAsync(ct);
        if (lastInstance == null) return null;

        var nextStart = ComputeNextStart(lastInstance.DueDate, rec.Periodicity);
        var nextDue = nextStart.AddMinutes(rec.DurationMinutes);

        var now = DateTimeOffset.UtcNow;
        var maxNum = await _db.TaskItems.MaxAsync(t => (int?)t.Number, ct) ?? 0;
        var next = new TaskItem
        {
            Id = Guid.NewGuid(),
            Number = maxNum + 1,
            Subject = rec.RootTask.Subject,
            Description = rec.RootTask.Description,
            Status = TaskStatus.New,
            Priority = rec.RootTask.Priority,
            CategoryId = rec.RootTask.CategoryId,
            AuthorUserId = rec.RootTask.AuthorUserId,
            AssigneeUserId = rec.RootTask.AssigneeUserId,
            StartDate = nextStart,
            DueDate = nextDue,
            ControlType = rec.RootTask.ControlType,
            ControllerUserId = rec.RootTask.ControllerUserId,
            Kind = TaskKind.Periodic,
            SeriesId = recurrenceId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.TaskItems.Add(next);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(), TaskId = next.Id, ActorUserId = rec.RootTask.AuthorUserId,
            Action = TaskHistoryAction.Created, NewValue = "periodic", CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);
        return next;
    }

    private static DateTimeOffset ComputeNextStart(DateTimeOffset lastDue, TaskPeriodicity periodicity)
        => periodicity switch
        {
            TaskPeriodicity.Daily or TaskPeriodicity.WorkingDays => lastDue.AddDays(1),
            TaskPeriodicity.Weekly => lastDue.AddDays(7),
            TaskPeriodicity.Monthly => lastDue.AddMonths(1),
            TaskPeriodicity.Quarterly => lastDue.AddMonths(3),
            TaskPeriodicity.Yearly => lastDue.AddYears(1),
            _ => lastDue.AddDays(1),
        };

    private static TaskRecurrenceDto MapRecurrenceDto(TaskRecurrence rec)
        => new()
        {
            Id = rec.Id,
            RootTaskId = rec.RootTaskId,
            Periodicity = rec.Periodicity.ToString(),
            EndCondition = rec.EndCondition.ToString(),
            EndDate = rec.EndDate,
            LookAheadCount = rec.LookAheadCount,
            DurationMinutes = rec.DurationMinutes,
            IsActive = rec.IsActive,
        };

    // ─── FR-TASK-01.4: Массовый контроль задач ────────────────────────────────

    /// <inheritdoc/>
    public async Task<int> BulkVerifyAsync(IReadOnlyList<Guid> taskIds, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        if (taskIds == null || taskIds.Count == 0)
            throw new ValidationException("Список задач не может быть пустым.");

        int accepted = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var taskId in taskIds)
        {
            var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task == null) continue;

            // Принять контроль можно только когда задача ожидает подтверждения
            if (task.Status != TaskStatus.DoneNeedsControl && task.Status != TaskStatus.CannotDoNeedsControl)
                continue;

            if (task.ControllerUserId != actorId && !isAdmin)
                continue;

            var oldStatus = task.Status;
            var newStatus = task.Status == TaskStatus.DoneNeedsControl
                ? TaskStatus.DoneControlled
                : TaskStatus.CannotDoControlled;

            task.Status = newStatus;
            task.UpdatedAt = now;
            AddStatusHistory(task.Id, actorId, oldStatus, newStatus, now);
            accepted++;
        }

        if (accepted > 0)
            await _db.SaveChangesAsync(ct);

        return accepted;
    }

    // ─── FR-TASK-01.5.2: Перенаправление ProcessTask при блокировке ──────────

    /// <inheritdoc/>
    public async Task ReassignBlockedProcessTasksAsync(Guid blockedUserId, CancellationToken ct = default)
    {
        // Находим все открытые ProcessTask-задачи, где исполнитель заблокирован
        var openStatuses = new[]
        {
            TaskStatus.New,
            TaskStatus.Read,
            TaskStatus.InProgress,
            TaskStatus.DoneNeedsControl,
            TaskStatus.CannotDoNeedsControl,
            TaskStatus.PreApproval,
            TaskStatus.OnApproval,
            TaskStatus.PreApprovalRejected,
            TaskStatus.ApprovalRejected,
            TaskStatus.Postponed,
        };

        var tasks = await _db.TaskItems
            .Where(t => t.Kind == TaskKind.ProcessTask
                     && t.AssigneeUserId == blockedUserId
                     && openStatuses.Contains(t.Status)
                     && t.SourceInstanceId != null)
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        // Загружаем связанные экземпляры процессов для получения инициатора
        var instanceIds = tasks.Select(t => t.SourceInstanceId!.Value).Distinct().ToList();
        var instances = await _db.BpmInstances.AsNoTracking()
            .Where(i => instanceIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var now = DateTimeOffset.UtcNow;
        var maxNum = await _db.TaskItems.MaxAsync(t => (int?)t.Number, ct) ?? 0;

        foreach (var task in tasks)
        {
            if (!instances.TryGetValue(task.SourceInstanceId!.Value, out var instance)) continue;

            // Новый исполнитель — инициатор экземпляра процесса (или ответственный, если инициатора нет)
            var newAssigneeId = instance.InitiatorUserId ?? instance.ResponsibleUserId;
            if (newAssigneeId == null || newAssigneeId == blockedUserId) continue;

            var oldAssigneeId = task.AssigneeUserId;
            task.AssigneeUserId = newAssigneeId.Value;
            task.UpdatedAt = now;

            _db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ActorUserId = Guid.Empty, // системное действие
                Action = TaskHistoryAction.Reassigned,
                OldValue = oldAssigneeId.ToString(),
                NewValue = newAssigneeId.Value.ToString(),
                CreatedAt = now,
            });

            // Уведомить нового исполнителя
            await _notifications.NotifyUserAsync(newAssigneeId.Value, "TaskAssigned",
                new { taskId = task.Id, taskNumber = task.Number, reason = "AssigneeBlocked" });
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── FR-TASK-02.1: Дополнительные действия ──────────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskDto> RescheduleAsync(Guid taskId, RescheduleTaskRequest req, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (FinalStatuses.Contains(task.Status))
            throw new ValidationException($"Нельзя перенести срок задачи в финальном статусе «{task.Status}».");

        var isAuthor = task.AuthorUserId == actorId;
        var isController = task.ControllerUserId == actorId;
        if (!isAuthor && !isController && !isAdmin)
            throw new ValidationException("Только автор, контролёр или администратор может перенести срок задачи.");

        if (req.NewDueDate <= task.DueDate || req.NewDueDate <= DateTimeOffset.UtcNow)
            throw new ValidationException("Новый срок должен быть в будущем и позже текущего срока задачи.");

        var now = DateTimeOffset.UtcNow;
        var oldDueDate = task.DueDate;
        task.DueDate = req.NewDueDate;
        task.UpdatedAt = now;

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.Rescheduled,
            OldValue = oldDueDate.ToString("O"),
            NewValue = req.NewDueDate.ToString("O"),
            CreatedAt = now,
        });

        if (!string.IsNullOrWhiteSpace(req.Comment))
            _db.TaskComments.Add(new TaskComment { Id = Guid.NewGuid(), TaskId = taskId, AuthorUserId = actorId, Body = req.Comment.Trim(), CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        // FR-TASK-02.3: Уведомление наблюдателей о переносе срока
        await NotifyObserversAsync(task, "TaskRescheduled", ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> ReopenAsync(Guid taskId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var reopenableStatuses = new HashSet<TaskStatus>
        {
            TaskStatus.Done, TaskStatus.DoneControlled, TaskStatus.CannotDo, TaskStatus.CannotDoControlled,
        };

        if (!reopenableStatuses.Contains(task.Status))
            throw new ValidationException($"Нельзя открыть заново задачу в статусе «{task.Status}».");

        var isAuthor = task.AuthorUserId == actorId;
        var isController = task.ControllerUserId == actorId;
        if (!isAuthor && !isController && !isAdmin)
            throw new ValidationException("Только автор, контролёр или администратор может открыть задачу заново.");

        var now = DateTimeOffset.UtcNow;
        var oldStatus = task.Status;
        task.Status = TaskStatus.New;
        task.UpdatedAt = now;
        AddStatusHistory(task.Id, actorId, oldStatus, TaskStatus.New, now);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.Reopened,
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(ct);

        // Уведомить исполнителя
        if (task.AssigneeUserId != actorId)
            await _notifications.NotifyUserAsync(task.AssigneeUserId, "TaskReopened",
                new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);

        // FR-TASK-02.3: Уведомление наблюдателей об открытии заново
        await NotifyObserversAsync(task, "TaskReopened", ct);

        return await BuildDtoAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> ClaimAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (FinalStatuses.Contains(task.Status))
            throw new ValidationException($"Нельзя взять задачу в статусе «{task.Status}».");

        if (task.AssigneeUserId == actorId)
            throw new ValidationException("Вы уже являетесь исполнителем задачи.");

        var now = DateTimeOffset.UtcNow;
        var oldAssigneeId = task.AssigneeUserId;
        task.AssigneeUserId = actorId;
        task.UpdatedAt = now;

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.Claimed,
            OldValue = oldAssigneeId.ToString(),
            NewValue = actorId.ToString(),
            CreatedAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return await BuildDtoAsync(taskId, ct);
    }

    // ─── FR-TASK-02.1: Наблюдатели ───────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskParticipantDto>> GetWatchersAsync(Guid taskId, CancellationToken ct = default)
    {
        var _ = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var watchers = await _db.TaskParticipants.AsNoTracking()
            .Where(p => p.TaskId == taskId && p.Role == TaskParticipantRole.Observer)
            .ToListAsync(ct);

        var userIds = watchers.Select(p => p.UserId).ToList();
        var users = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return watchers.Select(p => new TaskParticipantDto
        {
            Id = p.Id,
            UserId = p.UserId,
            UserName = users.GetValueOrDefault(p.UserId, p.UserId.ToString()),
            Role = p.Role.ToString(),
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskParticipantDto> AddWatcherAsync(Guid taskId, Guid watcherUserId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var existing = await _db.TaskParticipants
            .FirstOrDefaultAsync(p => p.TaskId == taskId && p.UserId == watcherUserId && p.Role == TaskParticipantRole.Observer, ct);
        if (existing != null)
        {
            var existingUser = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == watcherUserId, ct);
            return new TaskParticipantDto { Id = existing.Id, UserId = existing.UserId, UserName = existingUser?.DisplayName ?? watcherUserId.ToString(), Role = existing.Role.ToString() };
        }

        var now = DateTimeOffset.UtcNow;
        var participant = new TaskParticipant { Id = Guid.NewGuid(), TaskId = taskId, UserId = watcherUserId, Role = TaskParticipantRole.Observer, CreatedAt = now };
        _db.TaskParticipants.Add(participant);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.WatcherAdded,
            NewValue = watcherUserId.ToString(),
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        var user = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == watcherUserId, ct);
        return new TaskParticipantDto { Id = participant.Id, UserId = watcherUserId, UserName = user?.DisplayName ?? watcherUserId.ToString(), Role = TaskParticipantRole.Observer.ToString() };
    }

    /// <inheritdoc/>
    public async Task RemoveWatcherAsync(Guid taskId, Guid watcherUserId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var participant = await _db.TaskParticipants
            .FirstOrDefaultAsync(p => p.TaskId == taskId && p.UserId == watcherUserId && p.Role == TaskParticipantRole.Observer, ct);
        if (participant == null) return;

        // Разрешено: сам наблюдатель, автор задачи, исполнитель, контролёр, Admin
        var task = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct);
        var canRemove = isAdmin || actorId == watcherUserId
            || (task != null && (task.AuthorUserId == actorId || task.AssigneeUserId == actorId || task.ControllerUserId == actorId));
        if (!canRemove)
            throw new ValidationException("Недостаточно прав для удаления наблюдателя.");

        _db.TaskParticipants.Remove(participant);
        var now = DateTimeOffset.UtcNow;
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.WatcherRemoved,
            OldValue = watcherUserId.ToString(),
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);
    }

    // ─── FR-TASK-02.1: Вопросы ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskQuestionDto>> GetQuestionsAsync(Guid taskId, CancellationToken ct = default)
    {
        var _ = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var questions = await _db.TaskQuestions.AsNoTracking()
            .Where(q => q.TaskId == taskId)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync(ct);

        var userIds = questions.SelectMany(q => new[] { q.AuthorUserId, q.RecipientId }).Distinct().ToList();
        var users = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return questions.Select(q => new TaskQuestionDto
        {
            Id = q.Id,
            TaskId = q.TaskId,
            AuthorUserId = q.AuthorUserId,
            AuthorName = users.GetValueOrDefault(q.AuthorUserId, q.AuthorUserId.ToString()),
            RecipientId = q.RecipientId,
            RecipientName = users.GetValueOrDefault(q.RecipientId, q.RecipientId.ToString()),
            QuestionText = q.QuestionText,
            AnswerText = q.AnswerText,
            AnsweredAt = q.AnsweredAt,
            CreatedAt = q.CreatedAt,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskQuestionDto> AskQuestionAsync(Guid taskId, AskTaskQuestionRequest req, Guid authorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.QuestionText))
            throw new ValidationException("Текст вопроса обязателен.");
        if (req.RecipientId == Guid.Empty)
            throw new ValidationException("Получатель вопроса обязателен.");

        var task = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        var now = DateTimeOffset.UtcNow;
        var question = new TaskQuestion
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorUserId = authorId,
            RecipientId = req.RecipientId,
            QuestionText = req.QuestionText.Trim(),
            CreatedAt = now,
        };
        _db.TaskQuestions.Add(question);
        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = authorId,
            Action = TaskHistoryAction.QuestionAsked,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        // Уведомить получателя
        await _notifications.NotifyUserAsync(req.RecipientId, "TaskQuestion",
            new { taskId = task.Id, number = task.Number, subject = task.Subject, questionId = question.Id }, ct);

        // Автоматически добавить получателя в наблюдатели (если ещё нет)
        var alreadyWatcher = await _db.TaskParticipants
            .AnyAsync(p => p.TaskId == taskId && p.UserId == req.RecipientId && p.Role == TaskParticipantRole.Observer, ct);
        if (!alreadyWatcher)
            _db.TaskParticipants.Add(new TaskParticipant { Id = Guid.NewGuid(), TaskId = taskId, UserId = req.RecipientId, Role = TaskParticipantRole.Observer, CreatedAt = now });

        await _db.SaveChangesAsync(ct);

        var authorUser = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == authorId, ct);
        var recipientUser = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == req.RecipientId, ct);
        return new TaskQuestionDto
        {
            Id = question.Id,
            TaskId = taskId,
            AuthorUserId = authorId,
            AuthorName = authorUser?.DisplayName ?? authorId.ToString(),
            RecipientId = req.RecipientId,
            RecipientName = recipientUser?.DisplayName ?? req.RecipientId.ToString(),
            QuestionText = question.QuestionText,
            CreatedAt = question.CreatedAt,
        };
    }

    /// <inheritdoc/>
    public async Task<TaskQuestionDto> AnswerQuestionAsync(Guid taskId, Guid questionId, AnswerTaskQuestionRequest req, Guid actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.AnswerText))
            throw new ValidationException("Текст ответа обязателен.");

        var question = await _db.TaskQuestions
            .FirstOrDefaultAsync(q => q.Id == questionId && q.TaskId == taskId, ct)
            ?? throw new NotFoundException($"Вопрос {questionId} не найден.");

        if (question.RecipientId != actorId)
            throw new ValidationException("Только получатель вопроса может ответить на него.");

        if (question.AnswerText != null)
            throw new ValidationException("На вопрос уже дан ответ.");

        var now = DateTimeOffset.UtcNow;
        question.AnswerText = req.AnswerText.Trim();
        question.AnsweredAt = now;

        _db.TaskHistoryEntries.Add(new TaskHistoryEntry
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            ActorUserId = actorId,
            Action = TaskHistoryAction.QuestionAnswered,
            CreatedAt = now,
        });
        await _db.SaveChangesAsync(ct);

        // Уведомить автора вопроса об ответе
        await _notifications.NotifyUserAsync(question.AuthorUserId, "TaskQuestionAnswered",
            new { taskId, questionId = question.Id }, ct);

        var authorUser = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == question.AuthorUserId, ct);
        var recipientUser = await _db.OrgUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        return new TaskQuestionDto
        {
            Id = question.Id,
            TaskId = taskId,
            AuthorUserId = question.AuthorUserId,
            AuthorName = authorUser?.DisplayName ?? question.AuthorUserId.ToString(),
            RecipientId = actorId,
            RecipientName = recipientUser?.DisplayName ?? actorId.ToString(),
            QuestionText = question.QuestionText,
            AnswerText = question.AnswerText,
            AnsweredAt = question.AnsweredAt,
            CreatedAt = question.CreatedAt,
        };
    }

    // ─── Вспомогательный метод: уведомление соисполнителей ────────────────────

    private async Task NotifyCoExecutorsAsync(TaskItem task, string eventType, CancellationToken ct)
    {
        var coExecutors = task.Participants
            .Where(p => p.Role == TaskParticipantRole.CoExecutor)
            .Select(p => p.UserId)
            .Distinct();
        foreach (var userId in coExecutors)
            await _notifications.NotifyUserAsync(userId, eventType,
                new { taskId = task.Id, number = task.Number, subject = task.Subject }, ct);
    }
    // ─── FR-TASK-02.3: Напоминания ────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskReminderDto>> GetRemindersAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var reminders = await _db.TaskReminders.AsNoTracking()
            .Where(r => r.TaskId == taskId && r.UserId == userId)
            .OrderBy(r => r.RemindAt)
            .ToListAsync(ct);
        return reminders.Select(r => new TaskReminderDto
        {
            Id = r.Id,
            TaskId = r.TaskId,
            UserId = r.UserId,
            RemindAt = r.RemindAt,
            Note = r.Note,
            IsSent = r.IsSent,
            CreatedAt = r.CreatedAt,
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<TaskReminderDto> AddReminderAsync(Guid taskId, AddTaskReminderRequest req, Guid userId, CancellationToken ct = default)
    {
        _ = await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        if (req.RemindAt <= DateTimeOffset.UtcNow)
            throw new ValidationException("Дата напоминания должна быть в будущем.");

        var now = DateTimeOffset.UtcNow;
        var reminder = new TaskReminder
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            RemindAt = req.RemindAt,
            Note = req.Note?.Trim(),
            IsSent = false,
            CreatedAt = now,
        };
        _db.TaskReminders.Add(reminder);
        await _db.SaveChangesAsync(ct);

        return new TaskReminderDto
        {
            Id = reminder.Id,
            TaskId = taskId,
            UserId = userId,
            RemindAt = reminder.RemindAt,
            Note = reminder.Note,
            IsSent = false,
            CreatedAt = now,
        };
    }

    /// <inheritdoc/>
    public async Task DeleteReminderAsync(Guid reminderId, Guid actorId, CancellationToken ct = default)
    {
        var reminder = await _db.TaskReminders.FirstOrDefaultAsync(r => r.Id == reminderId && r.UserId == actorId, ct);
        if (reminder == null) return; // уже удалено или не принадлежит актору
        _db.TaskReminders.Remove(reminder);
        await _db.SaveChangesAsync(ct);
    }

    // ─── FR-TASK-02.3: Планирование в календаре ──────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskDto> ScheduleTaskAsync(Guid taskId, ScheduleTaskRequest req, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        task.ScheduledAt = req.ScheduledAt;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await NotifyObserversAsync(task, "TaskScheduled", ct);
        return await GetAsync(taskId, ct);
    }

    /// <inheritdoc/>
    public async Task<TaskDto> UnscheduleTaskAsync(Guid taskId, Guid actorId, CancellationToken ct = default)
    {
        var task = await _db.TaskItems.Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException($"Задача {taskId} не найдена.");

        task.ScheduledAt = null;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return await GetAsync(taskId, ct);
    }

    // ─── FR-TASK-02.3: Дашборд задач ─────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskDashboardDto> GetDashboardAsync(Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        IQueryable<TaskItem> query = _db.TaskItems.AsNoTracking();
        if (!isAdmin)
        {
            var participantTaskIds = _db.TaskParticipants
                .Where(p => p.UserId == userId)
                .Select(p => p.TaskId);
            query = query.Where(t => t.AuthorUserId == userId || t.AssigneeUserId == userId || participantTaskIds.Contains(t.Id));
        }

        var tasks = await query.ToListAsync(ct);
        var openTasks = tasks.Where(t => !FinalStatuses.Contains(t.Status)).ToList();

        var byStatus = openTasks
            .GroupBy(t => t.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byPriority = openTasks
            .GroupBy(t => t.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var cutoff = DateTimeOffset.UtcNow.AddDays(-30).Date;
        var recentTasks = tasks.Where(t => t.CreatedAt.Date >= cutoff).ToList();
        var closedRecent = tasks.Where(t => FinalStatuses.Contains(t.Status) && t.UpdatedAt.Date >= cutoff).ToList();

        var dailyStats = new List<TaskDailyStatDto>();
        for (var day = cutoff; day <= DateTimeOffset.UtcNow.Date; day = day.AddDays(1))
        {
            dailyStats.Add(new TaskDailyStatDto
            {
                Date = day.ToString("yyyy-MM-dd"),
                Created = recentTasks.Count(t => t.CreatedAt.Date == day),
                Closed = closedRecent.Count(t => t.UpdatedAt.Date == day),
            });
        }

        return new TaskDashboardDto
        {
            ByStatus = byStatus,
            ByPriority = byPriority,
            OverdueCount = openTasks.Count(t => t.IsOverdue),
            OpenCount = openTasks.Count,
            DailyStats = dailyStats,
        };
    }

    // ─── FR-TASK-02.3: Настройки уведомлений ─────────────────────────────────

    private static readonly string[] DefaultTaskEventTypes =
    [
        "TaskAssigned", "TaskDone", "TaskOverdue", "TaskCommentAdded",
        "TaskReminder", "TaskRescheduled", "TaskReopened", "TaskQuestionAsked",
        "TaskMentioned", "TaskCompleted", "TaskScheduled",
    ];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserTaskNotificationSettingsDto>> GetNotificationSettingsAsync(Guid userId, CancellationToken ct = default)
    {
        var saved = await _db.UserTaskNotificationSettings.AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        // Возвращаем дефолтные записи для событий, которые ещё не настраивались
        var result = new List<UserTaskNotificationSettingsDto>();
        foreach (var eventType in DefaultTaskEventTypes)
        {
            var existing = saved.FirstOrDefault(s => s.EventType == eventType);
            result.Add(new UserTaskNotificationSettingsDto
            {
                Id = existing?.Id ?? Guid.Empty,
                EventType = eventType,
                InApp = existing?.InApp ?? true,
                Email = existing?.Email ?? false,
            });
        }
        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserTaskNotificationSettingsDto>> UpdateNotificationSettingsAsync(
        Guid userId, IReadOnlyList<UpdateNotificationSettingRequest> settings, CancellationToken ct = default)
    {
        var existing = await _db.UserTaskNotificationSettings
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        foreach (var req in settings)
        {
            var entity = existing.FirstOrDefault(s => s.EventType == req.EventType);
            if (entity == null)
            {
                entity = new Domain.Tasks.UserTaskNotificationSettings
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EventType = req.EventType,
                };
                _db.UserTaskNotificationSettings.Add(entity);
            }
            entity.InApp = req.InApp;
            entity.Email = req.Email;
        }
        await _db.SaveChangesAsync(ct);
        return await GetNotificationSettingsAsync(userId, ct);
    }

    // ─── FR-TASK-02.2: Счётчики задач ────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<TaskCountersDto> GetCountersAsync(Guid userId, CancellationToken ct = default)
    {
        var finalStatuses = new[] { TaskStatus.Done, TaskStatus.DoneControlled, TaskStatus.CannotDo, TaskStatus.CannotDoControlled, TaskStatus.Closed };

        var incoming = await _db.TaskItems.AsNoTracking()
            .CountAsync(t => t.AssigneeUserId == userId && !finalStatuses.Contains(t.Status), ct);

        var overdue = await _db.TaskItems.AsNoTracking()
            .CountAsync(t => t.AssigneeUserId == userId && t.IsOverdue && !finalStatuses.Contains(t.Status), ct);

        var approverParticipantTaskIds = _db.TaskParticipants
            .Where(p => p.UserId == userId && p.Role == TaskParticipantRole.Approver)
            .Select(p => p.TaskId);
        var onApproval = await _db.TaskItems.AsNoTracking()
            .CountAsync(t => t.Status == TaskStatus.OnApproval && approverParticipantTaskIds.Contains(t.Id), ct);

        var needsControl = await _db.TaskItems.AsNoTracking()
            .CountAsync(t => t.ControllerUserId == userId
                && (t.Status == TaskStatus.DoneNeedsControl || t.Status == TaskStatus.CannotDoNeedsControl), ct);

        return new TaskCountersDto
        {
            Incoming = incoming,
            Overdue = overdue,
            OnApproval = onApproval,
            NeedsControl = needsControl,
        };
    }

    // ─── FR-TASK-02.2: Excel-экспорт ─────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<byte[]> ExportToExcelAsync(Guid userId, bool isAdmin, TaskListFilter filter, CancellationToken ct = default)
    {
        // Снимаем пагинацию для экспорта
        filter.Page = 1;
        filter.PageSize = 10_000;
        var tasks = await ListAsync(userId, isAdmin, filter, ct);

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Задачи");

        // Заголовки
        var headers = new[] { "№", "Тема", "Статус", "Приоритет", "Вид", "Исполнитель", "Автор", "Срок", "Категория", "Теги", "Создана" };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        int row = 2;
        foreach (var t in tasks)
        {
            ws.Cell(row, 1).Value = $"T-{t.Number}";
            ws.Cell(row, 2).Value = t.Subject;
            ws.Cell(row, 3).Value = t.Status;
            ws.Cell(row, 4).Value = t.Priority;
            ws.Cell(row, 5).Value = t.Kind;
            ws.Cell(row, 6).Value = t.AssigneeName;
            ws.Cell(row, 7).Value = t.AuthorName;
            ws.Cell(row, 8).Value = t.DueDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            ws.Cell(row, 9).Value = t.CategoryId ?? "";
            ws.Cell(row, 10).Value = string.Join(", ", t.Tags);
            ws.Cell(row, 11).Value = t.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            row++;
        }
        ws.Columns().AdjustToContents();

        using var ms = new System.IO.MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
