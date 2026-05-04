namespace CoreBPM.Server.Domain.Notify;

/// <summary>Эмодзи-реакция на публикацию канала (таблица notify_post_reactions, FR-MSG-01.2).</summary>
public class NotifyPostReaction
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Эмодзи-символ реакции (например «👍»).</summary>
    public string Emoji { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public NotifyChannelPost? Post { get; set; }
}
