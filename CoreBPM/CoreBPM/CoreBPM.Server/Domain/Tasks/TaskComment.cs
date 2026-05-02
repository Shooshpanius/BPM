namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Комментарий к задаче (таблица task_comments).</summary>
public class TaskComment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem Task { get; set; } = null!;
}
