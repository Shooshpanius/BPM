namespace CoreBPM.Server.Domain.Notify;

/// <summary>Закреплённое сообщение в чате (таблица notify_pinned_messages, FR-MSG-01.2).</summary>
public class NotifyPinnedMessage
{
    public Guid Id { get; set; }

    /// <summary>Чат, в котором закреплено сообщение.</summary>
    public Guid ChatId { get; set; }

    /// <summary>Идентификатор закреплённого сообщения.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Кто закрепил сообщение.</summary>
    public Guid PinnedByUserId { get; set; }

    public DateTimeOffset PinnedAt { get; set; }

    public NotifyChat? Chat { get; set; }
    public NotifyMessage? Message { get; set; }
}
