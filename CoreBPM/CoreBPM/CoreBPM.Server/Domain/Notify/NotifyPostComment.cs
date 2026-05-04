namespace CoreBPM.Server.Domain.Notify;

/// <summary>Комментарий к публикации канала (таблица notify_post_comments, FR-MSG-01.2).</summary>
public class NotifyPostComment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid AuthorUserId { get; set; }

    /// <summary>Текст комментария.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Комментарий был мягко удалён.</summary>
    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public NotifyChannelPost? Post { get; set; }
}
