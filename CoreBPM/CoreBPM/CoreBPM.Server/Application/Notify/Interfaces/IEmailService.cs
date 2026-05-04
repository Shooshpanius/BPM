namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис отправки email-уведомлений (FR-MSG-02.1).</summary>
public interface IEmailService
{
    /// <summary>Отправляет HTML-письмо. Не бросает исключение — логирует ошибки.</summary>
    Task SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Проверяет соединение с SMTP-сервером (для теста настроек).</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
