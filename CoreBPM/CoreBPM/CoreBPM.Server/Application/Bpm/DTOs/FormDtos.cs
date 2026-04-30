using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Application.Bpm.DTOs;

// ─── Форма ──────────────────────────────────────────────────────────────────

/// <summary>Краткое представление формы задачи для списка.</summary>
public record FormListItemDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ProcessId,
    string? ElementId,
    int TotalVersions,
    BpmFormVersionStatus? LatestVersionStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Полное представление формы задачи (без схемы версий).</summary>
public record FormDto(
    Guid Id,
    string Name,
    string? Description,
    Guid? ProcessId,
    string? ElementId,
    int TotalVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на создание формы.</summary>
public record CreateFormRequest(
    string Name,
    string? Description,
    Guid? ProcessId,
    string? ElementId
);

/// <summary>Запрос на обновление метаданных формы.</summary>
public record UpdateFormRequest(
    string Name,
    string? Description,
    Guid? ProcessId,
    string? ElementId
);

// ─── Версия формы ────────────────────────────────────────────────────────────

/// <summary>Краткая информация о версии формы (без схемы).</summary>
public record FormVersionInfoDto(
    Guid Id,
    int VersionNumber,
    BpmFormVersionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt
);

/// <summary>Полная версия формы со схемой.</summary>
public record FormVersionDto(
    Guid Id,
    Guid FormId,
    int VersionNumber,
    BpmFormVersionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    object Schema
);

/// <summary>Запрос на сохранение нового черновика формы.</summary>
public record SaveFormVersionRequest(
    /// <summary>JSON-схема формы — произвольный объект, хранится как текст.</summary>
    object Schema
);
