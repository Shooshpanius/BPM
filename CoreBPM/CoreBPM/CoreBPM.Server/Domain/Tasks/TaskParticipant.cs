namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Участник задачи (таблица task_participants).</summary>
public class TaskParticipant
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public TaskParticipantRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem Task { get; set; } = null!;
}
