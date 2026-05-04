namespace CoreBPM.Server.Domain.Notify;

/// <summary>Информационный канал (таблица notify_channels, FR-MSG-01.2).</summary>
public class NotifyChannel
{
    public Guid Id { get; set; }

    /// <summary>Название канала.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание канала.</summary>
    public string? Description { get; set; }

    /// <summary>Эмодзи-иконка канала.</summary>
    public string? IconEmoji { get; set; }

    /// <summary>Тип канала: публичный или приватный.</summary>
    public NotifyChannelKind Kind { get; set; }

    /// <summary>Создатель канала (автоматически становится администратором).</summary>
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<NotifyChannelSubscriber> Subscribers { get; set; } = new List<NotifyChannelSubscriber>();
    public ICollection<NotifyChannelPost> Posts { get; set; } = new List<NotifyChannelPost>();
}
