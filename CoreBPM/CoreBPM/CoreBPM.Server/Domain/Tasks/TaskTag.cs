namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Тег задачи (таблица task_tags).</summary>
public class TaskTag
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem Task { get; set; } = null!;
}
