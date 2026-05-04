namespace CoreBPM.Server.Domain.Notify;

/// <summary>
/// Настройки режима «Не беспокоить» для пользователя (таблица notify_dnd_settings, FR-MSG-02.2).
/// Определяет временные диапазоны и дни недели, в которые push и SMS не отправляются.
/// </summary>
public class NotifyDndSettings
{
    public Guid Id { get; set; }

    /// <summary>Пользователь, для которого настроен DND.</summary>
    public Guid UserId { get; set; }

    /// <summary>Включён ли режим «Не беспокоить».</summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>Час начала тихого режима (0–23, UTC).</summary>
    public int StartHour { get; set; } = 22;

    /// <summary>Час конца тихого режима (0–23, UTC).</summary>
    public int EndHour { get; set; } = 8;

    /// <summary>
    /// Дни недели, в которые действует DND.
    /// Хранится как строка с запятыми: "0,6" = воскресенье и суббота.
    /// 0=воскресенье, 1=понедельник … 6=суббота.
    /// </summary>
    public string DisabledDays { get; set; } = string.Empty;

    /// <summary>Временная зона пользователя (IANA, например Europe/Moscow).</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>Применять ли DND к push-уведомлениям.</summary>
    public bool ApplyToPush { get; set; } = true;

    /// <summary>Применять ли DND к SMS-уведомлениям.</summary>
    public bool ApplyToSms { get; set; } = true;

    /// <summary>Дата последнего изменения.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
