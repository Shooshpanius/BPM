namespace CoreBPM.Server.Domain.Tasks;

/// <summary>SLA-правило для задач (FR-TASK-01.2). Таблица task_sla.</summary>
public class TaskSlaRule
{
    public Guid Id { get; set; }
    /// <summary>Категория задачи (null — применяется ко всем категориям).</summary>
    public string? CategoryId { get; set; }
    /// <summary>Приоритет задачи (null — применяется ко всем приоритетам).</summary>
    public TaskPriority? Priority { get; set; }
    /// <summary>Срок по умолчанию в часах.</summary>
    public int DefaultDueHours { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
