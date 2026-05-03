namespace CoreBPM.Server.Domain.Tasks;

/// <summary>Запись о трудозатратах по задаче (FR-TASK-01.4).</summary>
public class TaskTimeLog
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;
    /// <summary>Пользователь, внёсший трудозатраты.</summary>
    public Guid UserId { get; set; }
    /// <summary>Вид деятельности (опциональный).</summary>
    public Guid? ActivityTypeId { get; set; }
    /// <summary>Длительность в минутах.</summary>
    public int DurationMinutes { get; set; }
    /// <summary>Дата начала работы.</summary>
    public DateTimeOffset StartDate { get; set; }
    /// <summary>Комментарий к трудозатрате.</summary>
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
