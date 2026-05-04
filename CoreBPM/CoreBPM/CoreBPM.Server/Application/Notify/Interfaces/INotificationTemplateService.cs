using CoreBPM.Server.Application.Notify.DTOs;

namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис управления глобальными шаблонами уведомлений (FR-MSG-02.2).</summary>
public interface INotificationTemplateService
{
    /// <summary>Получить список всех шаблонов.</summary>
    Task<IReadOnlyList<NotificationTemplateDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Получить шаблон по типу события (или null).</summary>
    Task<NotificationTemplateDto?> GetByEventTypeAsync(string eventType, CancellationToken ct = default);

    /// <summary>Создать или обновить шаблон для типа события.</summary>
    Task<NotificationTemplateDto> UpsertAsync(
        string eventType, UpsertNotificationTemplateRequest req, CancellationToken ct = default);

    /// <summary>Удалить шаблон.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Применить шаблон — подставить переменные в текст.
    /// variables: словарь {{key}} → value.
    /// </summary>
    string Render(string template, IDictionary<string, string> variables);

    /// <summary>
    /// Вернуть признаки принудительности для данного типа события.
    /// </summary>
    Task<(bool MandatoryInApp, bool MandatoryEmail, bool MandatorySms, bool MandatoryPush)>
        GetMandatoryFlagsAsync(string eventType, CancellationToken ct = default);
}
