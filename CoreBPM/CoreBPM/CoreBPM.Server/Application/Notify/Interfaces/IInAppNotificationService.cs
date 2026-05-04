using CoreBPM.Server.Application.Notify.DTOs;

namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис управления персистентными in-app уведомлениями (FR-MSG-02.1).</summary>
public interface IInAppNotificationService
{
    /// <summary>Сохраняет уведомление в inbox пользователя.</summary>
    Task SaveAsync(SaveInboxEntryRequest request, CancellationToken ct = default);

    /// <summary>Возвращает страницу уведомлений пользователя с фильтрами.</summary>
    Task<(IReadOnlyList<InboxEntryDto> Items, int Total)> GetPagedAsync(
        Guid userId,
        bool? isRead,
        string? type,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Возвращает количество непрочитанных уведомлений пользователя.</summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Отмечает уведомление прочитанным.</summary>
    Task MarkReadAsync(Guid userId, Guid entryId, CancellationToken ct = default);

    /// <summary>Отмечает все уведомления пользователя прочитанными.</summary>
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Удаляет уведомление пользователя.</summary>
    Task DeleteAsync(Guid userId, Guid entryId, CancellationToken ct = default);
}
