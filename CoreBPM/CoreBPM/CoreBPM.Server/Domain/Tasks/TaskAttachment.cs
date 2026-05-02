namespace CoreBPM.Server.Domain.Tasks;
/// <summary>Вложение к задаче (таблица task_attachments).</summary>
public class TaskAttachment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TaskItem Task { get; set; } = null!;
}
