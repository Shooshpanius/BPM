namespace CoreBPM.Server.Domain.Notify;

/// <summary>
/// Журнал последних отправок для проверки ограничения частоты (таблица notify_throttle_log, FR-MSG-02.2).
/// Хранит время последней успешной доставки уведомления по каждой комбинации
/// пользователь + тип события + канал, что позволяет проверять throttle.
/// </summary>
public class NotifyThrottleLog
{
    public Guid Id { get; set; }

    /// <summary>Пользователь-получатель.</summary>
    public Guid UserId { get; set; }

    /// <summary>Тип события.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Канал доставки.</summary>
    public DeliveryChannel Channel { get; set; }

    /// <summary>Время последней успешной отправки.</summary>
    public DateTimeOffset LastSentAt { get; set; } = DateTimeOffset.UtcNow;
}
