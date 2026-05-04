namespace CoreBPM.Server.Domain.Notify;

/// <summary>Подписчик канала (таблица notify_channel_subscribers, FR-MSG-01.2).</summary>
public class NotifyChannelSubscriber
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Является ли подписчик администратором/модератором канала.</summary>
    public bool IsAdmin { get; set; }

    public DateTimeOffset SubscribedAt { get; set; }

    public NotifyChannel? Channel { get; set; }
}
