using CoreBPM.Server.Application.Notify.DTOs;

namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис управления настройками DND и журналом доставки (FR-MSG-02.2).</summary>
public interface INotificationSettingsService
{
    // ── DND ──────────────────────────────────────────────────────────────────

    /// <summary>Получить настройки DND текущего пользователя.</summary>
    Task<DndSettingsDto> GetDndSettingsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Обновить настройки DND.</summary>
    Task<DndSettingsDto> UpdateDndSettingsAsync(Guid userId, DndSettingsDto dto, CancellationToken ct = default);

    /// <summary>Проверяет, находится ли текущий момент в режиме DND для данного пользователя.</summary>
    Task<bool> IsInDndAsync(Guid userId, CancellationToken ct = default);

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
}
