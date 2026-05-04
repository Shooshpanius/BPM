namespace CoreBPM.Server.Domain.Notify;

/// <summary>Публикация в информационном канале (таблица notify_channel_posts, FR-MSG-01.2).</summary>
public class NotifyChannelPost
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid AuthorUserId { get; set; }

    /// <summary>Заголовок публикации (опционально).</summary>
    public string? Title { get; set; }

    /// <summary>Тело публикации (rich text / Markdown).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Публикация была отредактирована.</summary>
    public bool IsEdited { get; set; }

    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public NotifyChannel? Channel { get; set; }
}
