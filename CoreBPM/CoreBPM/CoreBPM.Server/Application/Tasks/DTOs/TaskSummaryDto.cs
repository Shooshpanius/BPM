namespace CoreBPM.Server.Application.Tasks.DTOs;
public class TaskSummaryDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string? CategoryId { get; set; }
    public Guid AssigneeUserId { get; set; }
    public string AssigneeName { get; set; } = string.Empty;
    /// <summary>Автор задачи (FR-TASK-02.2).</summary>
    public Guid AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTimeOffset DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
    /// <summary>Вид задачи (Regular/Periodic/ProcessTask/Resolution). FR-TASK-02.2.</summary>
    public string Kind { get; set; } = "Regular";
    /// <summary>Дата планирования в календаре (FR-TASK-02.3).</summary>
    public DateTimeOffset? ScheduledAt { get; set; }
    /// <summary>Текущий пользователь является соисполнителем этой задачи. FR-TASK-02.2.</summary>
    public bool IsCoExecutor { get; set; }
    /// <summary>Количество незакрытых вопросов к задаче. FR-TASK-02.2.</summary>
    public int OpenQuestionCount { get; set; }
}
