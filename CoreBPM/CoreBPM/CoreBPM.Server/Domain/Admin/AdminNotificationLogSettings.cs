namespace CoreBPM.Server.Domain.Admin;

/// <summary>
/// Системные настройки хранения журнала доставки уведомлений (таблица admin_notification_log_settings, FR-MSG-02.2).
/// Singleton-запись (Id=1).
/// </summary>
public class AdminNotificationLogSettings
{
    public int Id { get; set; } = 1;

    /// <summary>Количество дней хранения записей журнала доставки (0 = хранить бессрочно).</summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>Дата последнего изменения.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
