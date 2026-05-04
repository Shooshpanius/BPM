namespace CoreBPM.Server.Domain.Notify;

/// <summary>Запись в почтовом ящике in-app уведомлений пользователя (таблица notify_inbox).</summary>
public class NotifyInboxEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор получателя.</summary>
    public Guid UserId { get; set; }

    /// <summary>Тип события (TaskAssigned, ChannelInvite, ImprovementStatusChanged и т.д.).</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Заголовок уведомления.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Текстовое тело уведомления.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Сериализованный JSON-объект с дополнительными данными.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Ссылка для перехода при клике на уведомление (необязательно).</summary>
    public string? Link { get; set; }

    /// <summary>Признак прочитанности.</summary>
    public bool IsRead { get; set; }

    /// <summary>Дата и время создания.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Дата и время прочтения.</summary>
    public DateTimeOffset? ReadAt { get; set; }
}
