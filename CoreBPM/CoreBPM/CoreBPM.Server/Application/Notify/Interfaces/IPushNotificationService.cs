namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис Web Push уведомлений (FR-MSG-02.1).</summary>
public interface IPushNotificationService
{
    /// <summary>Получить текущий VAPID публичный ключ (Base64url).</summary>
    Task<string?> GetVapidPublicKeyAsync(CancellationToken ct = default);

    /// <summary>Сгенерировать и сохранить новую VAPID ключевую пару.</summary>
    Task<string> GenerateVapidKeysAsync(CancellationToken ct = default);

    /// <summary>Добавить или обновить подписку пользователя.</summary>
    Task<Guid> SaveSubscriptionAsync(
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        CancellationToken ct = default);

    /// <summary>Удалить подписку по endpoint.</summary>
    Task DeleteSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct = default);

    /// <summary>Получить все подписки пользователя.</summary>
    Task<IReadOnlyList<PushSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Отправить push-уведомление всем подпискам пользователя.</summary>
    Task SendAsync(
        Guid userId,
        string title,
        string body,
        string? link,
        CancellationToken ct = default);
}

/// <summary>DTO подписки Web Push.</summary>
public sealed record PushSubscriptionDto(
    Guid Id,
    string Endpoint,
    string? UserAgent,
    DateTimeOffset CreatedAt
);

/// <summary>Запрос регистрации подписки.</summary>
public sealed record RegisterPushSubscriptionRequest(
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent
);
