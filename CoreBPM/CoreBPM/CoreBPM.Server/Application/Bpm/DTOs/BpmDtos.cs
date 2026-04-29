using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Application.Bpm.DTOs;

/// <summary>Краткое представление процесса для списка.</summary>
public record BpmProcessListItemDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    int? ActiveVersionNumber,
    int TotalVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Полное представление процесса.</summary>
public record BpmProcessDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    Guid CreatedByUserId,
    int? ActiveVersionNumber,
    int TotalVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на создание процесса.</summary>
public record CreateBpmProcessRequest(
    Guid OrganizationId,
    string Name,
    string? Description
);

/// <summary>Запрос на обновление метаданных процесса.</summary>
public record UpdateBpmProcessRequest(
    string Name,
    string? Description
);

/// <summary>Информация о версии диаграммы (без XML).</summary>
public record BpmProcessVersionInfoDto(
    Guid Id,
    int VersionNumber,
    BpmProcessVersionStatus Status,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Диаграмма версии процесса (с XML).</summary>
public record BpmDiagramDto(
    Guid VersionId,
    int VersionNumber,
    BpmProcessVersionStatus Status,
    string? DiagramXml,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на сохранение XML-диаграммы.</summary>
public record SaveDiagramRequest(string DiagramXml);

// ─── Конфигурации элементов ─────────────────────────────────────────────────

/// <summary>DTO конфигурации BPMN-элемента.</summary>
public record BpmElementConfigDto(
    string ElementId,
    string ConfigJson,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на создание / обновление конфигурации элемента.</summary>
public record UpsertElementConfigRequest(string ConfigJson);

// ─── Переменные процесса ─────────────────────────────────────────────────────

/// <summary>DTO переменной контекста процесса.</summary>
public record BpmProcessVariableDto(
    Guid Id,
    string Name,
    BpmVariableType VariableType,
    string? DefaultValue,
    bool IsKeyVariable,
    bool IsInput,
    bool IsOutput,
    int SortOrder
);

/// <summary>Запрос на создание переменной процесса.</summary>
public record CreateBpmVariableRequest(
    string Name,
    BpmVariableType VariableType,
    string? DefaultValue,
    bool IsKeyVariable,
    bool IsInput,
    bool IsOutput
);

/// <summary>Запрос на обновление переменной процесса.</summary>
public record UpdateBpmVariableRequest(
    string Name,
    BpmVariableType VariableType,
    string? DefaultValue,
    bool IsKeyVariable,
    bool IsInput,
    bool IsOutput
);

/// <summary>Запрос на изменение порядка переменных.</summary>
public record ReorderVariablesRequest(IReadOnlyList<Guid> OrderedIds);

// ─── RACI-матрица ─────────────────────────────────────────────────────────────

/// <summary>DTO записи RACI-матрицы.</summary>
public record BpmRaciEntryDto(
    Guid Id,
    string Stage,
    string Role,
    BpmRaciType RaciType
);

/// <summary>Запрос на создание / обновление записи RACI.</summary>
public record UpsertRaciEntryRequest(
    string Stage,
    string Role,
    BpmRaciType RaciType
);

