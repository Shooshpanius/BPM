namespace CoreBPM.Server.Domain.Notify;

/// <summary>Чат (таблица notify_chats, FR-MSG-01.1).</summary>
public class NotifyChat
{
    public Guid Id { get; set; }

    /// <summary>Название чата (для группового; null для DM).</summary>
    public string? Name { get; set; }

    /// <summary>Вид: личный (Direct) или групповой (Group).</summary>
    public NotifyChatKind Kind { get; set; }

    /// <summary>Идентификатор пользователя, создавшего чат.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Дата и время последнего сообщения в чате (для сортировки).</summary>
    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<NotifyChatMember> Members { get; set; } = new List<NotifyChatMember>();
    public ICollection<NotifyMessage> Messages { get; set; } = new List<NotifyMessage>();
    public ICollection<NotifyPinnedMessage> PinnedMessages { get; set; } = new List<NotifyPinnedMessage>();
}
