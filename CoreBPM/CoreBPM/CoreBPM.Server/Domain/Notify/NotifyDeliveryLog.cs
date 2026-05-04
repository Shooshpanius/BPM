namespace CoreBPM.Server.Domain.Notify;

/// <summary>Канал доставки системного уведомления (не путать с мессенджер-каналом NotifyChannel).</summary>
public enum DeliveryChannel
{
    InApp = 0,
    Email = 1,
    Sms = 2,
    Push = 3
}

/// <summary>Статус доставки уведомления.</summary>
public enum NotifyDeliveryStatus
{
    /// <summary>Успешно отправлено.</summary>
    Sent = 0,
    /// <summary>Ошибка при отправке.</summary>
    Failed = 1,
    /// <summary>Пропущено: пользователь отключил данный канал для данного типа события.</summary>
    SkippedUserSettings = 2,
    /// <summary>Пропущено: режим «Не беспокоить» активен.</summary>
    SkippedDnd = 3
}

/// <summary>
/// Журнал доставки уведомлений (таблица notify_delivery_log, FR-MSG-02.2).
/// Фиксирует каждую попытку доставки по каждому каналу.
/// </summary>
public class NotifyDeliveryLog
{
    public Guid Id { get; set; }

    /// <summary>Получатель уведомления.</summary>
    public Guid UserId { get; set; }

    /// <summary>Тип события (TaskAssigned, TaskDone и т.д.).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Канал доставки.</summary>
    public DeliveryChannel Channel { get; set; }

    /// <summary>Статус доставки.</summary>
    public NotifyDeliveryStatus Status { get; set; }

    /// <summary>Текст ошибки при Failed.</summary>
    public string? Error { get; set; }

    /// <summary>Время попытки доставки (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
