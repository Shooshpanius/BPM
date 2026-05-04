namespace CoreBPM.Server.Domain.Notify;

/// <summary>
/// Настройка ограничения частоты уведомлений (таблица notify_throttle_settings, FR-MSG-02.2).
/// Позволяет пользователю задать минимальный интервал между уведомлениями
/// одного типа по одному каналу доставки.
/// </summary>
public class NotifyThrottleSetting
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, для которого задано ограничение.</summary>
    public Guid UserId { get; set; }

    /// <summary>Тип события (TaskAssigned, TaskOverdue и т.д.).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Канал доставки (Email, Sms, Push, InApp).</summary>
    public DeliveryChannel Channel { get; set; }

    /// <summary>
    /// Минимальный интервал между отправками в минутах.
    /// 0 — ограничение отключено.
    /// </summary>
    public int MinIntervalMinutes { get; set; } = 0;

    /// <summary>Дата последнего изменения.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
