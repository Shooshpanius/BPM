namespace CoreBPM.Server.Application.Notify.DTOs;

/// <summary>DTO записи in-app уведомления.</summary>
public sealed record InboxEntryDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt
);

/// <summary>Запрос создания in-app уведомления.</summary>
public sealed record SaveInboxEntryRequest(
    Guid UserId,
    string Type,
    string Title,
    string Body,
    string? Link,
    string? PayloadJson
);

/// <summary>Настройки SMTP-сервера.</summary>
public sealed record SmtpSettingsDto(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string FromAddress,
    string FromName
);
