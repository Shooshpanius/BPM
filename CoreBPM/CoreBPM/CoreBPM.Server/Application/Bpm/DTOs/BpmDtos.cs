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
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt
);

/// <summary>Диаграмма версии процесса (с XML).</summary>
public record BpmDiagramDto(
    Guid VersionId,
    int VersionNumber,
    BpmProcessVersionStatus Status,
    string? DiagramXml,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt
);

/// <summary>Запрос на сохранение XML-диаграммы.</summary>
public record SaveDiagramRequest(string DiagramXml);

/// <summary>Запрос на валидацию процесса.</summary>
public record ValidateBpmProcessRequest(Guid? VersionId);

/// <summary>Результат проверки диаграммы процесса.</summary>
public record BpmValidationResultDto(
    Guid VersionId,
    int VersionNumber,
    IReadOnlyList<BpmValidationIssueDto> Issues
);

/// <summary>Ошибка или предупреждение валидации процесса.</summary>
public record BpmValidationIssueDto(
    string Severity,
    string Code,
    string Message,
    string? ElementId
);

/// <summary>Запрос на сравнение двух версий.</summary>
public record BpmVersionDiffRequest(Guid LeftVersionId, Guid RightVersionId);

/// <summary>Результат сравнения двух версий процесса.</summary>
public record BpmVersionDiffDto(
    Guid LeftVersionId,
    Guid RightVersionId,
    IReadOnlyList<BpmVersionDiffElementDto> Elements,
    IReadOnlyList<BpmVersionDiffPropertyDto> Properties
);

/// <summary>Изменение элемента на диаграмме.</summary>
public record BpmVersionDiffElementDto(
    string ChangeType,
    string ElementId,
    string ElementType,
    string? Name
);

/// <summary>Изменение свойства при сравнении версий.</summary>
public record BpmVersionDiffPropertyDto(
    string TargetType,
    string TargetId,
    string PropertyName,
    string? LeftValue,
    string? RightValue
);

/// <summary>Настройки процесса.</summary>
public record BpmProcessSettingsDto(
    Guid ProcessId,
    bool LaunchFromPortalEnabled,
    bool ShowInStartList,
    bool ExternalStartEnabled,
    IReadOnlyList<string> ExternalStartMethods,
    string? ExternalStartAllowedIps,
    bool HasExternalStartToken,
    string? ExternalStartTokenPreview,
    DateTimeOffset? ExternalStartTokenUpdatedAt,
    BpmInstanceNameMode InstanceNameMode,
    bool RequestInstanceNameOnStart,
    string? InstanceNameTemplate,
    string? KeyVariableName,
    string DataClassName,
    string DataTableName,
    string ProcessMetricsClassName,
    string ProcessMetricsTableName,
    string InstanceMetricsClassName,
    string InstanceMetricsTableName,
    bool SecondRuntimeEnabled,
    DateTimeOffset? SecondRuntimeUpgradedAt
);

/// <summary>Запрос на обновление настроек процесса.</summary>
public record UpdateBpmProcessSettingsRequest(
    bool LaunchFromPortalEnabled,
    bool ShowInStartList,
    bool ExternalStartEnabled,
    IReadOnlyList<string>? ExternalStartMethods,
    string? ExternalStartAllowedIps,
    BpmInstanceNameMode InstanceNameMode,
    bool RequestInstanceNameOnStart,
    string? InstanceNameTemplate,
    string? KeyVariableName,
    string? DataClassName,
    string? DataTableName,
    string? ProcessMetricsClassName,
    string? ProcessMetricsTableName,
    string? InstanceMetricsClassName,
    string? InstanceMetricsTableName,
    bool SecondRuntimeEnabled
);

/// <summary>Результат ротации токена внешнего запуска.</summary>
public record RotateExternalTokenResponse(
    string Token,
    string Preview,
    DateTimeOffset RotatedAt
);

/// <summary>Запрос на запуск debug-сессии процесса.</summary>
public record StartBpmDebugSessionRequest(
    Guid? VersionId,
    IReadOnlyDictionary<string, string>? Variables
);

/// <summary>Событие трассировки debug-сессии.</summary>
public record BpmDebugEventDto(
    DateTimeOffset Timestamp,
    string EventType,
    string? ElementId,
    string Message
);

/// <summary>Состояние debug-сессии процесса.</summary>
public record BpmDebugSessionDto(
    Guid SessionId,
    Guid ProcessId,
    Guid VersionId,
    int VersionNumber,
    bool IsCompleted,
    string? CurrentElementId,
    string? CurrentElementType,
    IReadOnlyDictionary<string, string> Variables,
    IReadOnlyList<BpmDebugEventDto> Events
);

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

// ─── Пользовательские статусы экземпляров процесса ───────────────────────────

/// <summary>DTO одного варианта пользовательского статуса экземпляра процесса.</summary>
public record InstanceStatusOptionDto(
    Guid Id,
    string Name,
    string Code,
    int SortOrder
);

/// <summary>DTO конфигурации статусов экземпляра процесса.</summary>
public record InstanceStatusConfigDto(
    Guid? LinkedVariableId,
    string? LinkedVariableName,
    BpmInterruptAction OnInterruptAction,
    string? OnInterruptScriptId,
    IReadOnlyList<InstanceStatusOptionDto> Options
);

/// <summary>Запрос на обновление конфигурации статусов.</summary>
public record UpdateStatusConfigRequest(
    Guid? LinkedVariableId,
    BpmInterruptAction OnInterruptAction,
    string? OnInterruptScriptId,
    /// <summary>Если true — создать новую переменную типа List с именем NewVariableName и привязать её.</summary>
    bool CreateVariable,
    string? NewVariableName
);

/// <summary>Запрос на создание нового варианта статуса.</summary>
public record CreateStatusOptionRequest(
    string Name,
    /// <summary>Если null — код генерируется автоматически транслитерацией из Name.</summary>
    string? Code
);

/// <summary>Запрос на обновление варианта статуса.</summary>
public record UpdateStatusOptionRequest(
    string Name,
    string Code
);

/// <summary>Запрос на изменение порядка вариантов статусов.</summary>
public record ReorderStatusOptionsRequest(IReadOnlyList<Guid> OrderedIds);
