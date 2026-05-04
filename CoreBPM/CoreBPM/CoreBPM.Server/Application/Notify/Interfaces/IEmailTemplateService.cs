namespace CoreBPM.Server.Application.Notify.Interfaces;

/// <summary>Сервис шаблонов email-уведомлений (FR-MSG-02.1 Rich email).</summary>
public interface IEmailTemplateService
{
    /// <summary>Получить все шаблоны.</summary>
    Task<IReadOnlyList<EmailTemplateDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Получить шаблон по типу события.</summary>
    Task<EmailTemplateDto?> GetByEventTypeAsync(string eventType, CancellationToken ct = default);

    /// <summary>Сохранить (upsert) шаблон.</summary>
    Task<EmailTemplateDto> UpsertAsync(UpsertEmailTemplateRequest request, CancellationToken ct = default);

    /// <summary>Удалить шаблон (сбросить к дефолтному).</summary>
    Task DeleteAsync(string eventType, CancellationToken ct = default);

    /// <summary>Сгенерировать HTML письма по шаблону (или дефолтному) для данного события.</summary>
    Task<(string Subject, string HtmlBody)> RenderAsync(
        string eventType,
        string title,
        string body,
        string? link,
        IReadOnlyList<EmailActionButton>? actions = null,
        CancellationToken ct = default);
}

/// <summary>DTO шаблона email.</summary>
public sealed record EmailTemplateDto(
    Guid Id,
    string EventType,
    string Subject,
    string HtmlTemplate,
    bool IsActive,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос создания/обновления шаблона.</summary>
public sealed record UpsertEmailTemplateRequest(
    string EventType,
    string Subject,
    string HtmlTemplate,
    bool IsActive
);

/// <summary>Кнопка действия в письме.</summary>
public sealed record EmailActionButton(
    string Label,
    string Url,
    string Color = "#3b82f6"
);
