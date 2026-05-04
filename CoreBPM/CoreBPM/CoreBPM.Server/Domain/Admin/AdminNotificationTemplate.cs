namespace CoreBPM.Server.Domain.Admin;

/// <summary>
/// Глобальный шаблон уведомления (таблица admin_notification_templates, FR-MSG-02.2).
/// Администратор настраивает тексты, каналы и признак обязательности для каждого типа события.
/// </summary>
public class AdminNotificationTemplate
{
    public Guid Id { get; set; }

    /// <summary>Тип события (TaskAssigned, TaskDone, ChatMessageReceived и т.д.).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Человекочитаемое название события.</summary>
    public string EventLabel { get; set; } = string.Empty;

    /// <summary>Тема email с переменными {{user.fullName}}, {{task.name}} и т.д.</summary>
    public string EmailSubjectTemplate { get; set; } = string.Empty;

    /// <summary>Тело email (HTML) с переменными.</summary>
    public string EmailBodyTemplate { get; set; } = string.Empty;

    /// <summary>Текст push/SMS уведомления с переменными.</summary>
    public string ShortTemplate { get; set; } = string.Empty;

    /// <summary>Обязательная доставка in-app (пользователь не может отключить).</summary>
    public bool IsMandatoryInApp { get; set; } = false;

    /// <summary>Обязательная доставка email (пользователь не может отключить).</summary>
    public bool IsMandatoryEmail { get; set; } = false;

    /// <summary>Обязательная доставка SMS (пользователь не может отключить).</summary>
    public bool IsMandatorySms { get; set; } = false;

    /// <summary>Обязательная доставка push (пользователь не может отключить).</summary>
    public bool IsMandatoryPush { get; set; } = false;

    /// <summary>Шаблон активен (неактивный шаблон не отображается пользователям).</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
