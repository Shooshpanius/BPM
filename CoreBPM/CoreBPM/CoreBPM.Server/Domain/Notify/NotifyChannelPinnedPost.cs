namespace CoreBPM.Server.Domain.Notify;

/// <summary>Закреплённая публикация информационного канала (таблица notify_channel_pinned_posts, FR-MSG-01.2).</summary>
public class NotifyChannelPinnedPost
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid PostId { get; set; }
    public Guid PinnedByUserId { get; set; }
    public DateTimeOffset PinnedAt { get; set; }

    public NotifyChannel? Channel { get; set; }
    public NotifyChannelPost? Post { get; set; }
}
