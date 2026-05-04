namespace CoreBPM.Server.Domain.Notify;

/// <summary>Эмодзи-реакция на сообщение (таблица notify_message_reactions, FR-MSG-01.1).</summary>
public class NotifyMessageReaction
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Эмодзи-символ реакции (например «👍»).</summary>
    public string Emoji { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public NotifyMessage? Message { get; set; }
}
