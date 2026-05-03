namespace CoreBPM.Server.Domain.Tasks;

/// <summary>
/// Конфигурация серии периодических задач (FR-TASK-01.5.1).
/// Таблица task_recurrences.
/// </summary>
public class TaskRecurrence
{
    public Guid Id { get; set; }

    /// <summary>Идентификатор «корневой» (первой) задачи серии.</summary>
    public Guid RootTaskId { get; set; }

    public TaskPeriodicity Periodicity { get; set; } = TaskPeriodicity.Daily;
    public TaskSeriesEndCondition EndCondition { get; set; } = TaskSeriesEndCondition.Never;

    /// <summary>Дата окончания серии (используется при EndCondition == ByDate).</summary>
    public DateTimeOffset? EndDate { get; set; }

    /// <summary>Сколько экземпляров создавать вперёд (0 — только при наступлении срока).</summary>
    public int LookAheadCount { get; set; } = 1;

    /// <summary>Длительность задачи в минутах (для расчёта DueDate следующего экземпляра).</summary>
    public int DurationMinutes { get; set; } = 480;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public TaskItem RootTask { get; set; } = null!;
}
