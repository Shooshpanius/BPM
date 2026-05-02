namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Задача (таблица task_items, FR-TASK-01.1).</summary>
public class TaskItem
{
    public Guid Id { get; set; }
    /// <summary>Порядковый номер задачи (T-N).</summary>
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.New;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public string? CategoryId { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid AssigneeUserId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset DueDate { get; set; }
    public TaskDateCorrectionMode DateCorrectionMode { get; set; } = TaskDateCorrectionMode.None;
    public int? PlannedEffortMinutes { get; set; }
    public TaskControlType ControlType { get; set; } = TaskControlType.None;
    public Guid? ControllerUserId { get; set; }
    public Guid? ParentTaskId { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset? PostponedUntil { get; set; }
    public Guid? SourceInstanceId { get; set; }
    public string? SourceElementId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public TaskItem? ParentTask { get; set; }
    public ICollection<TaskItem> Subtasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskParticipant> Participants { get; set; } = new List<TaskParticipant>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<TaskTag> Tags { get; set; } = new List<TaskTag>();
    public ICollection<TaskReminder> Reminders { get; set; } = new List<TaskReminder>();
}
