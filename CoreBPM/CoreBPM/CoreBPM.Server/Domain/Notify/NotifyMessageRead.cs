namespace CoreBPM.Server.Domain.Notify;

/// <summary>Факт прочтения сообщения пользователем (таблица notify_message_reads, FR-MSG-01.1).</summary>
public class NotifyMessageRead
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ReadAt { get; set; }

    public NotifyMessage? Message { get; set; }
}
