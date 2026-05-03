using CoreBPM.Server.Domain.Tasks;
namespace CoreBPM.Server.Application.Tasks.DTOs;

public class CreateTaskRequest
{
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public string? CategoryId { get; set; }
    public Guid AssigneeUserId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public TaskDateCorrectionMode DateCorrectionMode { get; set; } = TaskDateCorrectionMode.None;
    public int? PlannedEffortMinutes { get; set; }
    public TaskControlType ControlType { get; set; } = TaskControlType.None;
    public Guid? ControllerUserId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public Guid? ApproverId { get; set; }
    public List<Guid> CoExecutorIds { get; set; } = new();
    public List<Guid> ObserverIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset? ReminderAt { get; set; }
    // Поля для типизации задачи (FR-TASK-01.5)
    public TaskKind Kind { get; set; } = TaskKind.Regular;
    public Guid? DocumentId { get; set; }
}

public class UpdateTaskRequest
{
    public string? Subject { get; set; }
    public string? Description { get; set; }
    public TaskPriority? Priority { get; set; }
    public string? CategoryId { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public int? PlannedEffortMinutes { get; set; }
    public TaskControlType? ControlType { get; set; }
    public Guid? ControllerUserId { get; set; }
}

public class ReassignTaskRequest
{
    public Guid AssigneeUserId { get; set; }
    public string? Comment { get; set; }
}

public class AddTaskParticipantRequest
{
    public Guid UserId { get; set; }
    public TaskParticipantRole Role { get; set; }
}

public class AddTaskCommentRequest
{
    public string Body { get; set; } = string.Empty;
}

public class AddTaskRelationRequest
{
    public Guid TargetTaskId { get; set; }
    public TaskRelationType RelationType { get; set; }
}

public class AddTaskTagRequest
{
    public string Value { get; set; } = string.Empty;
}

public class AddTaskAttachmentRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

public class TaskListFilter
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? CategoryId { get; set; }
    public string? TagValue { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public bool? IsOverdue { get; set; }
    public string? Search { get; set; }
    public string SortBy { get; set; } = "created_at";
    public string SortDir { get; set; } = "desc";
}

/// <summary>Запрос на откладывание задачи (FR-TASK-01.2).</summary>
public class PostponeTaskRequest
{
    public DateTimeOffset PostponeUntil { get; set; }
    public string? Comment { get; set; }
}

// ─── FR-TASK-01.3: Согласование ─────────────────────────────────────────────

/// <summary>Запрос на отправку задачи на согласование (FR-TASK-01.3).</summary>
public class SendForApprovalRequest
{
    /// <summary>Идентификатор согласующего. Необязателен, если согласующий уже назначен участником.</summary>
    public Guid? ApproverId { get; set; }
    public string? Comment { get; set; }
}

/// <summary>Запрос-решение по согласованию с опциональным комментарием (FR-TASK-01.3).</summary>
public class ApprovalDecisionRequest
{
    public string? Comment { get; set; }
}

// ─── FR-TASK-01.5: Типы задач ────────────────────────────────────────────────

/// <summary>Запрос на создание периодической задачи (FR-TASK-01.5.1).</summary>
public class CreatePeriodicTaskRequest : CreateTaskRequest
{
    public TaskPeriodicity Periodicity { get; set; } = TaskPeriodicity.Daily;
    public TaskSeriesEndCondition EndCondition { get; set; } = TaskSeriesEndCondition.Never;
    public DateTimeOffset? EndDate { get; set; }
    public int LookAheadCount { get; set; } = 1;
    public int DurationMinutes { get; set; } = 480;
}

/// <summary>Запрос на создание задачи по резолюции документа (FR-TASK-01.5.3).</summary>
public class CreateResolutionTaskRequest : CreateTaskRequest
{
    /// <summary>Идентификатор документа, по которому наложена резолюция.</summary>
    public new Guid DocumentId { get; set; }
}

/// <summary>Запрос на обновление серии периодических задач (FR-TASK-01.5.1).</summary>
public class UpdateSeriesRequest
{
    public TaskPeriodicity? Periodicity { get; set; }
    public TaskSeriesEndCondition? EndCondition { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int? LookAheadCount { get; set; }
    public int? DurationMinutes { get; set; }
}
