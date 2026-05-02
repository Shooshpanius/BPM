namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Запись истории изменений задачи (таблица task_history).</summary>
public class TaskHistoryEntry
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid ActorUserId { get; set; }
    public TaskHistoryAction Action { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem Task { get; set; } = null!;
}
