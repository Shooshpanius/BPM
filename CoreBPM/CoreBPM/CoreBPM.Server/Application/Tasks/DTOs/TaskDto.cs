using CoreBPM.Server.Domain.Tasks;
namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public string DateCorrectionMode { get; set; } = string.Empty;
    public int? PlannedEffortMinutes { get; set; }
    /// <summary>Фактические трудозатраты — сумма всех записей (FR-TASK-01.4).</summary>
    public int ActualEffortMinutes { get; set; }
    public string ControlType { get; set; } = string.Empty;
    public Guid? ControllerUserId { get; set; }
    public string? ControllerName { get; set; }
    /// <summary>Согласующий (участник с ролью Approver), если назначен (FR-TASK-01.3).</summary>
    public Guid? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public Guid? ParentTaskId { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset? PostponedUntil { get; set; }
    public Guid? SourceInstanceId { get; set; }
    public string? SourceElementId { get; set; }
    // ─── FR-TASK-01.5: Типы задач ─────────────────────────────────────────
    /// <summary>Вид задачи (Regular/Periodic/ProcessTask/Resolution).</summary>
    public string Kind { get; set; } = "Regular";
    /// <summary>Ссылка на документ (для задачи по резолюции, FR-TASK-01.5.3).</summary>
    public Guid? DocumentId { get; set; }
    /// <summary>Ссылка на серию периодических задач (FR-TASK-01.5.1).</summary>
    public Guid? SeriesId { get; set; }
    /// <summary>Детали процесса (заполняется для задач вида ProcessTask, FR-TASK-01.5.2).</summary>
    public ProcessTaskInfoDto? ProcessInfo { get; set; }
    /// <summary>Детали серии (заполняется для задач вида Periodic, FR-TASK-01.5.1).</summary>
    public TaskRecurrenceDto? Recurrence { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<TaskParticipantDto> Participants { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public int SubtaskCount { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }
}

/// <summary>Детали задачи по процессу (FR-TASK-01.5.2).</summary>
public class ProcessTaskInfoDto
{
    public Guid InstanceId { get; set; }
    public string InstanceTitle { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ProcessVersionNumber { get; set; } = string.Empty;
    public DateTimeOffset LaunchedAt { get; set; }
    public Guid InitiatorUserId { get; set; }
    public string InitiatorName { get; set; } = string.Empty;
    public Guid? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
}

/// <summary>Конфигурация серии периодических задач (FR-TASK-01.5.1).</summary>
public class TaskRecurrenceDto
{
    public Guid Id { get; set; }
    public Guid RootTaskId { get; set; }
    public string Periodicity { get; set; } = string.Empty;
    public string EndCondition { get; set; } = string.Empty;
    public DateTimeOffset? EndDate { get; set; }
    public int LookAheadCount { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Задача в серии (для списка экземпляров периодической задачи).</summary>
public class PeriodicSeriesItemDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public bool IsOverdue { get; set; }
}
