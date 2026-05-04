using CoreBPM.Server.Application.Notify.DTOs;

namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис управления настройками DND, throttle, журналом доставки и статистикой (FR-MSG-02.2).</summary>
public interface INotificationSettingsService
{
    // ── DND ──────────────────────────────────────────────────────────────────

    /// <summary>Получить настройки DND текущего пользователя.</summary>
    Task<DndSettingsDto> GetDndSettingsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Обновить настройки DND.</summary>
    Task<DndSettingsDto> UpdateDndSettingsAsync(Guid userId, DndSettingsDto dto, CancellationToken ct = default);

    /// <summary>Проверяет, находится ли текущий момент в режиме DND для данного пользователя.</summary>
    Task<bool> IsInDndAsync(Guid userId, CancellationToken ct = default);

    // ── Throttle (ограничение частоты) ────────────────────────────────────

    /// <summary>Получить список throttle-настроек пользователя.</summary>
    Task<IReadOnlyList<ThrottleSettingDto>> GetThrottleSettingsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Обновить throttle-настройки пользователя.</summary>
    Task<IReadOnlyList<ThrottleSettingDto>> UpdateThrottleSettingsAsync(
        Guid userId, UpdateThrottleSettingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Проверяет, заблокирована ли отправка по throttle.
    /// Если заблокирована — возвращает true, иначе обновляет время последней отправки и возвращает false.
    /// </summary>
    Task<bool> IsThrottledAsync(
        Guid userId, string eventType, Domain.Notify.DeliveryChannel channel, CancellationToken ct = default);

    // ── Журнал доставки ───────────────────────────────────────────────────────

    /// <summary>Записать событие в журнал доставки.</summary>
    Task LogDeliveryAsync(
        Guid userId,
        string eventType,
        Domain.Notify.DeliveryChannel channel,
        Domain.Notify.NotifyDeliveryStatus status,
        string? error = null,
        CancellationToken ct = default);

    /// <summary>Получить записи журнала доставки (администратор).</summary>
    Task<(IReadOnlyList<DeliveryLogEntryDto> Items, int Total)> GetDeliveryLogsAsync(
        DeliveryLogFilterRequest filter, CancellationToken ct = default);

    // ── Retention (хранение журнала) ─────────────────────────────────────────

    /// <summary>Получить настройки хранения журнала.</summary>
    Task<NotificationLogRetentionDto> GetRetentionSettingsAsync(CancellationToken ct = default);

    /// <summary>Обновить настройки хранения журнала.</summary>
    Task<NotificationLogRetentionDto> UpdateRetentionSettingsAsync(
        NotificationLogRetentionDto dto, CancellationToken ct = default);

    /// <summary>Удалить записи журнала старше заданного количества дней.</summary>
    Task<int> PurgeOldLogsAsync(int olderThanDays, CancellationToken ct = default);

    // ── Статистика доставки ──────────────────────────────────────────────────

    /// <summary>Получить сводную статистику доставки уведомлений.</summary>
    Task<DeliveryStatsDto> GetDeliveryStatsAsync(
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}

