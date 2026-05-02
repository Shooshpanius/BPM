namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Напоминание по задаче (таблица task_reminders).</summary>
public class TaskReminder
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset RemindAt { get; set; }
    public bool IsSent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem Task { get; set; } = null!;
}
