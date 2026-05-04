using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebPush;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Domain.Admin;
using CoreBPM.Server.Domain.Notify;
using CoreBPM.Server.Infrastructure.Persistence;
using System.Text.Json;

namespace CoreBPM.Server.Application.Notify.Services;

/// <summary>Сервис Web Push уведомлений через VAPID (FR-MSG-02.1).</summary>
public class PushNotificationService : IPushNotificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(AppDbContext db, ILogger<PushNotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> GetVapidPublicKeyAsync(CancellationToken ct = default)
    {
        var settings = await _db.AdminVapidSettings.FirstOrDefaultAsync(ct);
        return settings?.PublicKey;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateVapidKeysAsync(CancellationToken ct = default)
    {
        var keys = VapidHelper.GenerateVapidKeys();

        var settings = await _db.AdminVapidSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new AdminVapidSettings();
            _db.AdminVapidSettings.Add(settings);
        }
        settings.PublicKey = keys.PublicKey;
        settings.PrivateKey = keys.PrivateKey;
        await _db.SaveChangesAsync(ct);

        return keys.PublicKey;
    }

    /// <inheritdoc/>
    public async Task<Guid> SaveSubscriptionAsync(
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? userAgent,
        CancellationToken ct = default)
    {
        // Обновляем существующую подписку с тем же endpoint
        var existing = await _db.NotifyPushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);

        if (existing is not null)
        {
            existing.P256dh = p256dh;
            existing.Auth = auth;
            existing.UserAgent = userAgent;
        }
        else
        {
            existing = new NotifyPushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
            };
            _db.NotifyPushSubscriptions.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing.Id;
    }

    /// <inheritdoc/>
    public async Task DeleteSubscriptionAsync(Guid userId, string endpoint, CancellationToken ct = default)
    {
        var sub = await _db.NotifyPushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
        if (sub is not null)
        {
            _db.NotifyPushSubscriptions.Remove(sub);
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PushSubscriptionDto>> GetUserSubscriptionsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        return await _db.NotifyPushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new PushSubscriptionDto(s.Id, s.Endpoint, s.UserAgent, s.CreatedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task SendAsync(
        Guid userId,
        string title,
        string body,
        string? link,
        CancellationToken ct = default)
    {
        var vapid = await _db.AdminVapidSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        if (vapid is null || string.IsNullOrWhiteSpace(vapid.PublicKey) || string.IsNullOrWhiteSpace(vapid.PrivateKey))
        {
            _logger.LogWarning("VAPID не настроен, push не отправлен для пользователя {UserId}", userId);
            return;
        }

        var subscriptions = await _db.NotifyPushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new
        {
            title,
            body,
            icon = "/icon-192.png",
            url = link ?? "/notifications",
        });

        var expiredEndpoints = new List<string>();

        foreach (var sub in subscriptions)
        {
            try
            {
                var pushSub = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                var vapidDetails = new VapidDetails(vapid.Subject, vapid.PublicKey, vapid.PrivateKey);
                var client = new WebPushClient();
                await client.SendNotificationAsync(pushSub, payload, vapidDetails);
                _logger.LogDebug("Push отправлен: {UserId} → {Endpoint}", userId, sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
            }
            catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                           || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Подписка устарела — удаляем
                expiredEndpoints.Add(sub.Endpoint);
                _logger.LogInformation("Push-подписка устарела, удаляем: {Endpoint}", sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки push: {UserId} → {Endpoint}", userId, sub.Endpoint[..Math.Min(50, sub.Endpoint.Length)]);
            }
        }

        // Удаляем устаревшие подписки
        if (expiredEndpoints.Count > 0)
        {
            var toDelete = await _db.NotifyPushSubscriptions
                .Where(s => s.UserId == userId && expiredEndpoints.Contains(s.Endpoint))
                .ToListAsync(ct);
            _db.NotifyPushSubscriptions.RemoveRange(toDelete);
            await _db.SaveChangesAsync(ct);
        }
    }
}
