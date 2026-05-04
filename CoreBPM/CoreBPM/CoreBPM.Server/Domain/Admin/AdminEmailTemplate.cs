namespace CoreBPM.Server.Domain.Admin;

/// <summary>Шаблон email-уведомления, редактируемый администратором (таблица admin_email_templates).</summary>
public class AdminEmailTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Тип события (TaskAssigned, ChannelInvite и т.д.). Уникальный ключ.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Тема письма. Поддерживает переменные {{title}}, {{body}}, {{user.fullName}}.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>HTML-тело письма. Поддерживает переменные {{title}}, {{body}}, {{link}}, {{actions}}.</summary>
    public string HtmlTemplate { get; set; } = string.Empty;

    /// <summary>Шаблон активен; если false — используется дефолтный HTML.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
