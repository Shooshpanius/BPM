namespace CoreBPM.Server.Domain.Notify;

/// <summary>Сообщение в чате (таблица notify_messages, FR-MSG-01.1).</summary>
public class NotifyMessage
{
    public Guid Id { get; set; }

    /// <summary>Идентификатор чата (если сообщение отправлено в чат/DM).</summary>
    public Guid? ChatId { get; set; }

    /// <summary>Автор сообщения.</summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>Текст сообщения (поддерживает Markdown и @упоминания).</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Сообщение было отредактировано.</summary>
    public bool IsEdited { get; set; }

    /// <summary>Дата последнего редактирования.</summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>Сообщение помечено как удалённое (soft delete).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Ссылка на сообщение, на которое отвечает это (threading).</summary>
    public Guid? ReplyToMessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public NotifyChat? Chat { get; set; }
    public NotifyMessage? ReplyToMessage { get; set; }
    public ICollection<NotifyMessageRead> Reads { get; set; } = new List<NotifyMessageRead>();
    public ICollection<NotifyMessageReaction> Reactions { get; set; } = new List<NotifyMessageReaction>();
}
