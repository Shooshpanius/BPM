namespace CoreBPM.Server.Domain.Notify;

/// <summary>Запись лога отправки SMS (таблица notify_sms_log).</summary>
public class NotifySmsLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор пользователя-получателя (может быть null для системных SMS).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Номер телефона получателя.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Тип события, вызвавшего отправку.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Текст отправленного сообщения.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Статус: Sent, Failed, Skipped.</summary>
    public string Status { get; set; } = "Sent";

    /// <summary>Ответ провайдера (HTTP-статус или тело ответа).</summary>
    public string? ProviderResponse { get; set; }

    /// <summary>Текст ошибки, если Status=Failed.</summary>
    public string? ErrorMessage { get; set; }

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
