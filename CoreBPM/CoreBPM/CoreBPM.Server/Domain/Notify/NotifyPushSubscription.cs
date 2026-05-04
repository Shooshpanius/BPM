namespace CoreBPM.Server.Domain.Notify;

/// <summary>Подписка браузера на Web Push уведомления (таблица notify_push_subscriptions).</summary>
public class NotifyPushSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Идентификатор пользователя.</summary>
    public Guid UserId { get; set; }

    /// <summary>Endpoint URL для отправки push-уведомлений (из PushSubscription.endpoint).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Ключ P256DH (Base64url) из PushSubscription.getKey("p256dh").</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Auth-секрет (Base64url) из PushSubscription.getKey("auth").</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>User-Agent браузера (для отображения в управлении подписками).</summary>
    public string? UserAgent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
