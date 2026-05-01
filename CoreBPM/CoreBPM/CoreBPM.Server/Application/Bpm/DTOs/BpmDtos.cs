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
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> Tags,
    bool IsTemplate
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
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> Tags,
    bool IsTemplate
);

/// <summary>Запрос на создание процесса.</summary>
public record CreateBpmProcessRequest(
    Guid OrganizationId,
    string Name,
    string? Description,
    IReadOnlyList<string>? Tags,
    bool IsTemplate = false
);

/// <summary>Запрос на обновление метаданных процесса.</summary>
public record UpdateBpmProcessRequest(
    string Name,
    string? Description,
    IReadOnlyList<string>? Tags,
    bool IsTemplate = false
);

/// <summary>Запрос на создание процесса из шаблона.</summary>
public record CreateProcessFromTemplateRequest(
    Guid OrganizationId,
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
    DateTimeOffset? PublishedAt,
    string? ReleaseNotes
);

/// <summary>Диаграмма версии процесса (с XML).</summary>
public record BpmDiagramDto(
    Guid VersionId,
    int VersionNumber,
    BpmProcessVersionStatus Status,
    string? DiagramXml,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    string? ReleaseNotes
);

/// <summary>Запрос на сохранение XML-диаграммы.</summary>
public record SaveDiagramRequest(string DiagramXml);

/// <summary>Запрос на публикацию версии (с опциональным комментарием).</summary>
public record PublishVersionRequest(string? ReleaseNotes);

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
    DateTimeOffset? SecondRuntimeUpgradedAt,
    // KPI-цели процесса (FR-BPM-03.2)
    double? TargetCycleTimeMinutes,
    double? TargetOnTimePercent,
    decimal? TargetCostPerInstance
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
    bool SecondRuntimeEnabled,
    // KPI-цели процесса (FR-BPM-03.2)
    double? TargetCycleTimeMinutes = null,
    double? TargetOnTimePercent = null,
    decimal? TargetCostPerInstance = null
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

// ─── Сценарии процессов ──────────────────────────────────────────────────────

/// <summary>DTO модуля сценариев версии процесса.</summary>
public record BpmScriptModuleDto(
    Guid Id,
    Guid ProcessVersionId,
    string ScriptBody,
    string Language,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt
);

/// <summary>Краткая информация об опубликованном модуле сценариев процесса (для выбора в настройках).</summary>
public record BpmScriptModuleSummaryDto(
    Guid Id,
    Guid VersionId,
    int VersionNumber,
    string? ReleaseNotes,
    DateTimeOffset PublishedAt
);

/// <summary>Запрос на сохранение сценария.</summary>
public record SaveScriptModuleRequest(string? ScriptBody, string Language = "CSharp");

/// <summary>Информация о версии процесса со статусом сценария (для списка в разделе «Сценарии»).</summary>
public record BpmProcessVersionScriptInfoDto(
    Guid ProcessId,
    string ProcessName,
    Guid VersionId,
    int VersionNumber,
    BpmProcessVersionStatus VersionStatus,
    bool HasScript,
    DateTimeOffset? ScriptPublishedAt
);

// ─── Пользовательские расширения дизайнера ──────────────────────────────────

/// <summary>DTO пользовательского расширения палитры дизайнера.</summary>
public record BpmDesignerExtensionDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    string? FolderPath,
    string ScriptBody,
    bool IsPublished,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на создание расширения.</summary>
public record CreateDesignerExtensionRequest(
    Guid OrganizationId,
    string Name,
    string? Description,
    string? FolderPath,
    string ScriptBody
);

/// <summary>Запрос на обновление расширения.</summary>
public record UpdateDesignerExtensionRequest(
    string Name,
    string? Description,
    string? FolderPath,
    string ScriptBody
);

// ─── Глобальные модули ───────────────────────────────────────────────────────

/// <summary>DTO глобального модуля (краткая версия).</summary>
public record BpmGlobalModuleDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string? Description,
    bool IsPublished,
    int FilesCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt
);

/// <summary>Запрос на создание глобального модуля.</summary>
public record CreateGlobalModuleRequest(
    Guid OrganizationId,
    string Name,
    string? Description
);

/// <summary>Запрос на обновление глобального модуля.</summary>
public record UpdateGlobalModuleRequest(
    string Name,
    string? Description
);

/// <summary>DTO файла глобального модуля.</summary>
public record BpmGlobalModuleFileDto(
    Guid Id,
    Guid ModuleId,
    string FileName,
    string ScriptBody,
    int Order,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на создание файла модуля.</summary>
public record CreateGlobalModuleFileRequest(string FileName, string ScriptBody);

/// <summary>Запрос на обновление файла модуля.</summary>
public record UpdateGlobalModuleFileRequest(string FileName, string ScriptBody);

/// <summary>Запрос на изменение порядка файлов модуля.</summary>
public record ReorderGlobalModuleFilesRequest(IReadOnlyList<Guid> OrderedIds);

// ─── Блокировки диаграмм ─────────────────────────────────────────────────────

/// <summary>Информация об активной блокировке диаграммы процесса.</summary>
public record DiagramLockDto(
    Guid ProcessId,
    Guid LockedByUserId,
    string LockedByDisplayName,
    DateTimeOffset LockedAt,
    DateTimeOffset LockedUntil
);

/// <summary>Ответ на попытку захвата блокировки диаграммы.</summary>
public record AcquireLockResponse(
    /// <summary>true — блокировка успешно захвачена (или продлена).</summary>
    bool IsAcquired,
    DiagramLockDto? Lock
);

// ─── Роли в процессе ─────────────────────────────────────────────────────────

/// <summary>DTO настройки роли в определении процесса.</summary>
public record BpmProcessRoleConfigDto(
    Guid Id,
    BpmProcessRoleType RoleType,
    BpmAssigneeType AssigneeType,
    string AssigneeId,
    string DisplayName,
    int SortOrder
);

/// <summary>Запрос на замену всех ролей процесса.</summary>
public record UpsertProcessRoleConfigsRequest(
    IReadOnlyList<UpsertProcessRoleConfigItem> Items
);

/// <summary>Элемент запроса на создание/обновление одной записи роли процесса.</summary>
public record UpsertProcessRoleConfigItem(
    BpmProcessRoleType RoleType,
    BpmAssigneeType AssigneeType,
    string AssigneeId,
    string DisplayName,
    int SortOrder
);

// ─── Экземпляры процесса ─────────────────────────────────────────────────────

/// <summary>Краткое представление экземпляра процесса для списка.</summary>
public record BpmInstanceListItemDto(
    Guid Id,
    Guid ProcessId,
    string ProcessName,
    Guid ProcessVersionId,
    int ProcessVersionNumber,
    string Name,
    BpmInstanceState State,
    BpmInstanceLaunchSource LaunchSource,
    Guid? InitiatorUserId,
    string? InitiatorDisplayName,
    Guid? ResponsibleUserId,
    string? ResponsibleDisplayName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt
);

/// <summary>Полное представление экземпляра процесса.</summary>
public record BpmInstanceDto(
    Guid Id,
    Guid ProcessId,
    string ProcessName,
    Guid ProcessVersionId,
    int ProcessVersionNumber,
    string Name,
    BpmInstanceState State,
    BpmInstanceLaunchSource LaunchSource,
    Guid? InitiatorUserId,
    string? InitiatorDisplayName,
    Guid? ResponsibleUserId,
    string? ResponsibleDisplayName,
    Guid? ParentInstanceId,
    string? ExternalReference,
    string? CancelReason,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<BpmInstanceVariableDto> Variables
);

/// <summary>DTO переменной экземпляра.</summary>
public record BpmInstanceVariableDto(
    Guid Id,
    string Name,
    string? ValueJson
);

/// <summary>Запрос на создание (запуск) экземпляра процесса.</summary>
public record CreateInstanceRequest(
    /// <summary>Название экземпляра (если не задана схема автоформирования).</summary>
    string? Name,
    /// <summary>Начальные значения переменных: словарь имя→JSON-значение.</summary>
    IDictionary<string, string?>? Variables,
    /// <summary>Внешний идентификатор корреляции (для вебхуков).</summary>
    string? ExternalReference = null
);

/// <summary>Запрос на создание экземпляра через вебхук.</summary>
public record WebhookLaunchRequest(
    /// <summary>Словарь переменных из тела запроса внешней системы.</summary>
    IDictionary<string, string?>? Variables,
    string? ExternalReference = null
);

/// <summary>DTO задания планировщика (таймерное стартовое событие).</summary>
public record BpmSchedulerJobDto(
    Guid Id,
    Guid ProcessId,
    Guid ProcessVersionId,
    string ElementId,
    string TimerType,
    string TimerValue,
    string? TimeZone,
    bool IsActive,
    DateTimeOffset? LastFiredAt,
    DateTimeOffset? NextFireAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);


// ─── Управление экземпляром ───────────────────────────────────────────────────

/// <summary>Запрос на прерывание (отмену) экземпляра процесса.</summary>
public record CancelInstanceRequest(
    /// <summary>Обязательная причина прерывания.</summary>
    string Reason
);

/// <summary>Запрос на смену ответственного за экземпляр.</summary>
public record ChangeResponsibleRequest(
    Guid NewResponsibleUserId
);

/// <summary>Запрос на обновление значения переменной экземпляра.</summary>
public record UpdateInstanceVariableRequest(
    string? ValueJson
);

/// <summary>Запрос на добавление комментария / вопроса к экземпляру.</summary>
public record AddCommentRequest(
    string Text,
    bool IsQuestion = false
);

/// <summary>Запрос на добавление участника экземпляра.</summary>
public record AddParticipantRequest(
    Guid UserId
);

/// <summary>DTO записи истории экземпляра.</summary>
public record BpmInstanceHistoryEntryDto(
    Guid Id,
    BpmHistoryEventType EventType,
    Guid? ActorUserId,
    string? ActorDisplayName,
    string? Text,
    string? MetaJson,
    DateTimeOffset OccurredAt
);

/// <summary>DTO участника экземпляра.</summary>
public record BpmInstanceParticipantDto(
    Guid Id,
    Guid UserId,
    string? DisplayName,
    Guid? AddedByUserId,
    string? AddedByDisplayName,
    DateTimeOffset AddedAt
);

// ─── Мои процессы (FR-BPM-02.3) ──────────────────────────────────────────────

/// <summary>Параметры фильтра для раздела «Мои процессы».</summary>
public record MyInstancesFilter(
    /// <summary>Роль пользователя в экземпляре.</summary>
    MyInstancesRole Role = MyInstancesRole.All,
    /// <summary>Фильтр по состоянию экземпляра (null = все состояния).</summary>
    BpmInstanceState? State = null,
    /// <summary>Строка поиска по названию экземпляра.</summary>
    string? Search = null,
    /// <summary>Фильтр по идентификатору процесса.</summary>
    Guid? ProcessId = null,
    /// <summary>Нижняя граница даты запуска.</summary>
    DateTimeOffset? DateFrom = null,
    /// <summary>Верхняя граница даты запуска.</summary>
    DateTimeOffset? DateTo = null
);

/// <summary>Результат запроса «Мои процессы» с общим счётчиком.</summary>
public record MyInstancesResult(
    IReadOnlyList<BpmInstanceListItemDto> Items,
    int Total
);

/// <summary>DTO сохранённого фильтра.</summary>
public record BpmSavedFilterDto(
    Guid Id,
    string Name,
    string FiltersJson,
    DateTimeOffset CreatedAt
);

/// <summary>Запрос на создание или обновление сохранённого фильтра.</summary>
public record SaveFilterRequest(
    string Name,
    string FiltersJson
);

// ─── Монитор процессов (FR-BPM-02.4) ─────────────────────────────────────────

/// <summary>Элемент списка монитора процессов: процесс + статистика экземпляров.</summary>
public record BpmProcessMonitorItemDto(
    Guid ProcessId,
    string ProcessName,
    string? ProcessDescription,
    int? ActiveVersionNumber,
    DateTimeOffset? PublishedAt,
    int ActiveCount,
    int SuspendedCount,
    int CompletedCount,
    int CancelledCount,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Curators
);

/// <summary>Детальная статистика экземпляров для страницы монитора процесса.</summary>
public record BpmProcessStatsDto(
    int ActiveCount,
    int SuspendedCount,
    int CompletedCount,
    int CancelledCount,
    int TotalCount,
    string ProcessName,
    string? ProcessDescription,
    int? ActiveVersionNumber,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Curators
);

// ─── Очередь исполнения и аналитика (FR-BPM-02.5) ────────────────────────────

/// <summary>DTO задания в очереди исполнения.</summary>
public record BpmExecutionJobDto(
    Guid Id,
    Guid ProcessId,
    string ProcessName,
    Guid? InstanceId,
    string? InstanceName,
    string ElementId,
    string ElementType,
    string? OperationName,
    BpmJobStatus Status,
    int AttemptNumber,
    int MaxAttempts,
    DateTimeOffset? NextRunAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? LastError,
    string? ServerHost,
    bool IsTimer,
    DateTimeOffset? TimerDeadline,
    DateTimeOffset CreatedAt
);

/// <summary>Агрегированные счётчики по статусам очереди.</summary>
public record QueueStatsDto(
    int Pending,
    int Running,
    int Scheduled,
    int Failed,
    int Total
);

/// <summary>Аналитика выполнения узла процесса.</summary>
public record NodeAnalyticsDto(
    string ElementId,
    string? ElementName,
    int ExecutionCount,
    double AvgDurationMs,
    double P50DurationMs,
    double P95DurationMs,
    int ErrorCount
);

/// <summary>Запрос на перенос времени запуска таймера.</summary>
public record RescheduleTimerRequest(DateTimeOffset NewRunAt);

// ─── Документирование процессов (FR-BPM-02.6) ────────────────────────────────

/// <summary>Процесс в подразделе «Документирование» с его опубликованными версиями.</summary>
public record ProcessDocumentationItemDto(
    Guid ProcessId,
    string ProcessName,
    string? ProcessDescription,
    bool IsDeleted,
    string[] Tags,
    IReadOnlyList<ProcessDocVersionDto> PublishedVersions
);

/// <summary>Запись об опубликованной версии в таблице документации.</summary>
public record ProcessDocVersionDto(
    Guid VersionId,
    int VersionNumber,
    DateTimeOffset? PublishedAt,
    Guid PublishedByUserId,
    string? ReleaseNotes,
    bool HasSnapshot
);

/// <summary>HTML-снапшот документации версии процесса.</summary>
public record DocSnapshotDto(
    Guid SnapshotId,
    Guid ProcessId,
    string ProcessName,
    Guid ProcessVersionId,
    int VersionNumber,
    DateTimeOffset GeneratedAt,
    string HtmlContent
);

// ──────────────────────────────────────────────────────────────────────────────
// FR-BPM-02.7: Пакеты миграции версий
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Краткое представление пакета миграции для списка.</summary>
public record MigrationPackageListItemDto(
    Guid Id,
    string Name,
    Guid CreatedByUserId,
    string CreatedByUserName,
    BpmMigrationPackageStatus Status,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int TotalItems,
    int MigratedItems,
    int ErrorItems
);

/// <summary>Полное представление пакета миграции с суммарной статистикой.</summary>
public record MigrationPackageDetailDto(
    Guid Id,
    string Name,
    Guid CreatedByUserId,
    string CreatedByUserName,
    BpmMigrationPackageStatus Status,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    int TotalItems,
    int MigratedItems,
    int ErrorItems,
    int PendingItems
);

/// <summary>Представление элемента пакета миграции.</summary>
public record MigrationItemDto(
    Guid Id,
    Guid PackageId,
    Guid InstanceId,
    string InstanceName,
    Guid ProcessId,
    string ProcessName,
    Guid TargetVersionId,
    int TargetVersionNumber,
    BpmMigrationItemStatus Status,
    string? ErrorComment,
    string? ManualChangeUrl,
    DateTimeOffset? ProcessedAt
);

/// <summary>Запрос на создание пакета миграции.</summary>
public record CreateMigrationPackageRequest(
    string Name,
    IReadOnlyList<MigrationItemRequest> Items
);

/// <summary>Элемент запроса на создание пакета миграции.</summary>
public record MigrationItemRequest(
    Guid InstanceId,
    Guid TargetVersionId
);

/// <summary>Запрос на ручную обработку элемента миграции.</summary>
public record ManualMigrateItemRequest(
    string? ManualChangeUrl
);

// ─── Пакетный запуск экземпляров (FR-BPM-02.1) ───────────────────────────────

/// <summary>Один элемент пакетного запуска: название и переменные для одного экземпляра.</summary>
public record BatchLaunchItem(
    string? Name,
    IDictionary<string, string?>? Variables
);

/// <summary>Запрос на пакетный запуск нескольких экземпляров одного процесса.</summary>
public record BatchLaunchRequest(
    IReadOnlyList<BatchLaunchItem> Items
);

/// <summary>Результат запуска одного экземпляра в пакете.</summary>
public record BatchLaunchItemResult(
    bool Success,
    Guid? InstanceId,
    string? InstanceName,
    string? Error
);

/// <summary>Итоговый результат пакетного запуска.</summary>
public record BatchLaunchResult(
    int Total,
    int Created,
    int Failed,
    IReadOnlyList<BatchLaunchItemResult> Items
);

// ─── Прямое переключение версии экземпляра (FR-BPM-02.2) ─────────────────────

/// <summary>Запрос на прямое переключение версии работающего экземпляра.</summary>
public record SwitchInstanceVersionRequest(
    Guid TargetVersionId
);

// ─── Дашборд мониторинга (FR-BPM-02.4) ───────────────────────────────────────

/// <summary>Сводная статистика по процессам для дашборда мониторинга.</summary>
public record BpmDashboardDto(
    int TotalProcesses,
    int ActiveInstances,
    int SuspendedInstances,
    int CompletedInstances,
    int CancelledInstances,
    int FaultedInstances,
    int FailedJobs,
    IReadOnlyList<BpmDashboardTopProcessDto> TopActiveProcesses
);

/// <summary>Строка дашборда: процесс с наибольшим числом активных экземпляров.</summary>
public record BpmDashboardTopProcessDto(
    Guid ProcessId,
    string ProcessName,
    int ActiveCount,
    int TotalCount
);


// ─── Движок выполнения BPMN (FR-BPM Execution Engine) ────────────────────────

/// <summary>DTO токена выполнения BPMN.</summary>
public record BpmTokenDto(
    Guid Id,
    Guid InstanceId,
    string ElementId,
    string ElementType,
    string? ElementName,
    CoreBPM.Server.Domain.Bpm.BpmTokenStatus Status,
    string? SignalCode,
    string? MessageCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt
);

/// <summary>Запрос на завершение UserTask/ReceiveTask.</summary>
public record CompleteUserTaskRequest(
    /// <summary>Выходные переменные задачи (имя → JSON-значение).</summary>
    IDictionary<string, string?>? OutputVariables = null
);

/// <summary>Запрос на отправку сигнала.</summary>
public record SendSignalRequest(
    /// <summary>Код сигнала BPMN.</summary>
    string SignalCode
);

/// <summary>Запрос на отправку сообщения.</summary>
public record SendMessageRequest(
    /// <summary>Код сообщения BPMN.</summary>
    string MessageCode,
    /// <summary>Ключ корреляции (опционально).</summary>
    string? CorrelationKey = null
);

// ─── FR-BPM-03.1: Предложения по улучшению ──────────────────────────────────

/// <summary>Запрос на создание предложения по улучшению процесса.</summary>
public record CreateImprovementRequest(
    string Subject,
    string? Description,
    Guid? SourceInstanceId = null,
    string? SourceTaskElementId = null
);

/// <summary>Запрос на принятие предложения (владелец процесса).</summary>
public record AcceptImprovementRequest(
    Guid AssignedUserId,
    DateTimeOffset DueDate,
    string? Comment = null
);

/// <summary>Запрос на отклонение предложения (владелец процесса).</summary>
public record RejectImprovementRequest(
    string? Comment = null
);

/// <summary>Запрос на завершение реализации улучшения (исполнитель).</summary>
public record CompleteImprovementRequest(
    string Resolution
);

/// <summary>Полное представление предложения по улучшению.</summary>
public record ImprovementDto(
    Guid Id,
    Guid ProcessId,
    string ProcessName,
    string Subject,
    string? Description,
    CoreBPM.Server.Domain.Bpm.BpmImprovementStatus Status,
    Guid InitiatorUserId,
    string InitiatorDisplayName,
    Guid? AssignedUserId,
    string? AssignedDisplayName,
    DateTimeOffset? DueDate,
    string? ReviewComment,
    string? Resolution,
    Guid? SourceInstanceId,
    string? SourceTaskElementId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? CompletedAt
);

/// <summary>Запись монитора улучшений — процесс с количеством предложений по статусам.</summary>
public record ImprovementMonitorItemDto(
    Guid ProcessId,
    string ProcessName,
    int PendingCount,
    int AcceptedCount,
    int InProgressCount,
    int CompletedCount,
    int RejectedCount,
    int TotalCount,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Curators
);

// ─── FR-BPM-03.2: Аналитика и KPI процессов ─────────────────────────────────

/// <summary>Сегмент гистограммы распределения времени цикла.</summary>
public record CycleTimeHistogramBucketDto(
    double FromMinutes,
    double ToMinutes,
    int Count
);

/// <summary>Аналитика процесса за период.</summary>
public record ProcessAnalyticsDto(
    Guid ProcessId,
    string ProcessName,
    int TotalInstances,
    int CompletedInstances,
    int FaultedInstances,
    double OnTimePercent,
    double FaultedPercent,
    double AvgCycleTimeMinutes,
    double MedianCycleTimeMinutes,
    double P95CycleTimeMinutes,
    IReadOnlyList<CycleTimeHistogramBucketDto> CycleTimeHistogram,
    double? TargetCycleTimeMinutes,
    double? TargetOnTimePercent
);

/// <summary>Сравнение KPI двух версий процесса.</summary>
public record ProcessVersionComparisonDto(
    Guid ProcessId,
    string ProcessName,
    Guid VersionAId,
    int VersionANumber,
    Guid VersionBId,
    int VersionBNumber,
    ProcessAnalyticsDto VersionAAnalytics,
    ProcessAnalyticsDto VersionBAnalytics
);

/// <summary>Строка сводного отчёта по всем процессам.</summary>
public record ProcessAnalyticsSummaryItemDto(
    Guid ProcessId,
    string ProcessName,
    int TotalInstances,
    double AvgCycleTimeMinutes,
    double OnTimePercent,
    double FaultedPercent,
    double? TargetCycleTimeMinutes,
    double? TargetOnTimePercent
);

/// <summary>Запись тепловой карты узла процесса.</summary>
public record NodeHeatMapDto(
    string ElementId,
    string ElementName,
    double AvgDurationMs,
    int PassCount,
    double HeatLevel
);

/// <summary>Шаг воронки процесса.</summary>
public record ProcessFunnelStepDto(
    string ElementId,
    string ElementName,
    int ReachedCount,
    int PassedCount,
    double DropOffPercent
);

/// <summary>Точка тренда KPI процесса за период (FR-BPM-03.2).</summary>
public record ProcessTrendPointDto(
    DateTimeOffset PeriodStart,
    int TotalInstances,
    double AvgCycleTimeMinutes,
    double OnTimePercent
);

/// <summary>DTO для KPI-алерта превышения порогового значения (FR-BPM-03.2).</summary>
public record KpiAlertDto(
    Guid Id,
    Guid ProcessId,
    string ProcessName,
    double AvgCycleTimeMinutes,
    double TargetCycleTimeMinutes,
    double ExceedPercent,
    DateTimeOffset DetectedAt
);
