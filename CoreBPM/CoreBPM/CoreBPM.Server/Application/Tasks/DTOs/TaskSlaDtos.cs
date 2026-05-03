namespace CoreBPM.Server.Application.Tasks.DTOs;

/// <summary>DTO SLA-правила задачи (FR-TASK-01.2).</summary>
public class TaskSlaRuleDto
{
    public Guid Id { get; set; }
    /// <summary>Категория задачи (null — применяется ко всем категориям).</summary>
    public string? CategoryId { get; set; }
    /// <summary>Приоритет задачи (null — применяется ко всем приоритетам).</summary>
    public string? Priority { get; set; }
    /// <summary>Срок по умолчанию в часах.</summary>
    public int DefaultDueHours { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Запрос создания/обновления SLA-правила.</summary>
public class UpsertTaskSlaRuleRequest
{
    public string? CategoryId { get; set; }
    /// <summary>Приоритет: Low, Medium, High, Critical (null — любой).</summary>
    public string? Priority { get; set; }
    public int DefaultDueHours { get; set; }
}
