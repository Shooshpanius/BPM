namespace CoreBPM.Server.Application.Notify.DTOs;

/// <summary>DTO записи in-app уведомления.</summary>
public sealed record InboxEntryDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt
);

/// <summary>Запрос создания in-app уведомления.</summary>
public sealed record SaveInboxEntryRequest(
    Guid UserId,
    string Type,
    string Title,
    string Body,
    string? Link,
    string? PayloadJson
);

/// <summary>Настройки SMTP-сервера.</summary>
public sealed record SmtpSettingsDto(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string FromAddress,
    string FromName
);

// ─── FR-MSG-02.2: DND ──────────────────────────────────────────────────────

/// <summary>DTO настроек режима «Не беспокоить».</summary>
public class DndSettingsDto
{
    public bool IsEnabled { get; set; }
    public int StartHour { get; set; }
    public int EndHour { get; set; }
    /// <summary>Список дней недели (0=вс, 1=пн … 6=сб).</summary>
    public List<int> DisabledDays { get; set; } = [];
    public string TimeZone { get; set; } = "UTC";
    public bool ApplyToPush { get; set; }
    public bool ApplyToSms { get; set; }
}

// ─── FR-MSG-02.2: Шаблоны уведомлений ────────────────────────────────────

/// <summary>DTO глобального шаблона уведомления (администратор).</summary>
public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventLabel { get; set; } = string.Empty;
    public string EmailSubjectTemplate { get; set; } = string.Empty;
    public string EmailBodyTemplate { get; set; } = string.Empty;
    public string ShortTemplate { get; set; } = string.Empty;
    public bool IsMandatoryInApp { get; set; }
    public bool IsMandatoryEmail { get; set; }
    public bool IsMandatorySms { get; set; }
    public bool IsMandatoryPush { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Запрос на создание/обновление шаблона уведомления.</summary>
public class UpsertNotificationTemplateRequest
{
    public string EventLabel { get; set; } = string.Empty;
    public string EmailSubjectTemplate { get; set; } = string.Empty;
    public string EmailBodyTemplate { get; set; } = string.Empty;
    public string ShortTemplate { get; set; } = string.Empty;
    public bool IsMandatoryInApp { get; set; }
    public bool IsMandatoryEmail { get; set; }
    public bool IsMandatorySms { get; set; }
    public bool IsMandatoryPush { get; set; }
    public bool IsActive { get; set; } = true;
}

// ─── FR-MSG-02.2: Журнал доставки ─────────────────────────────────────────

/// <summary>Запись журнала доставки уведомления.</summary>
public class DeliveryLogEntryDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Параметры фильтрации журнала доставки.</summary>
public class DeliveryLogFilterRequest
{
    public Guid? UserId { get; set; }
    public string? EventType { get; set; }
    public string? Channel { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

// ─── FR-MSG-02.2: Ограничение частоты (Throttle) ──────────────────────────

/// <summary>DTO одной настройки ограничения частоты уведомлений.</summary>
public class ThrottleSettingDto
{
    /// <summary>Тип события.</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>Канал: InApp, Email, Sms, Push.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>Минимальный интервал в минутах (0 = без ограничения).</summary>
    public int MinIntervalMinutes { get; set; } = 0;
}

/// <summary>Запрос обновления throttle-настроек пользователя.</summary>
public class UpdateThrottleSettingsRequest
{
    public List<ThrottleSettingDto> Settings { get; set; } = [];
}

// ─── FR-MSG-02.2: Настройки хранения журнала (Retention) ──────────────────

/// <summary>DTO настроек хранения журнала доставки уведомлений.</summary>
public class NotificationLogRetentionDto
{
    /// <summary>Количество дней хранения (0 = бессрочно).</summary>
    public int RetentionDays { get; set; } = 90;
}

// ─── FR-MSG-02.2: Статистика доставки ─────────────────────────────────────

/// <summary>Статистика доставки уведомлений по каналу.</summary>
public class DeliveryStatDto
{
    /// <summary>Канал доставки.</summary>
    public string Channel { get; set; } = string.Empty;
    /// <summary>Количество успешных отправок.</summary>
    public int Sent { get; set; }
    /// <summary>Количество ошибок.</summary>
    public int Failed { get; set; }
    /// <summary>Пропущено по настройкам пользователя.</summary>
    public int SkippedUserSettings { get; set; }
    /// <summary>Пропущено по DND.</summary>
    public int SkippedDnd { get; set; }
    /// <summary>Пропущено по throttle.</summary>
    public int SkippedThrottle { get; set; }
    /// <summary>Итого попыток.</summary>
    public int Total { get; set; }
}

/// <summary>Сводная статистика доставки уведомлений (администратор).</summary>
public class DeliveryStatsDto
{
    /// <summary>Начало периода.</summary>
    public DateTimeOffset? From { get; set; }
    /// <summary>Конец периода.</summary>
    public DateTimeOffset? To { get; set; }
    /// <summary>Статистика по каналам.</summary>
    public List<DeliveryStatDto> ByChannel { get; set; } = [];
    /// <summary>Топ-10 типов событий по количеству отправок.</summary>
    public List<EventTypeStatDto> TopEventTypes { get; set; } = [];
}

/// <summary>Статистика по типу события.</summary>
public class EventTypeStatDto
{
    public string EventType { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
}
