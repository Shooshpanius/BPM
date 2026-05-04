namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис SMS-уведомлений (FR-MSG-02.1).</summary>
public interface ISmsService
{
    /// <summary>Отправить SMS на указанный номер.</summary>
    Task SendAsync(
        string phoneNumber,
        string message,
        string eventType,
        Guid? userId = null,
        CancellationToken ct = default);

    /// <summary>Проверить подключение к SMS-провайдеру.</summary>
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
