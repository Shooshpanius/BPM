using CoreBPM.Server.Domain.Rules;

namespace CoreBPM.Server.Application.Rules.DTOs;

// ─── Таблица ────────────────────────────────────────────────────────────────

/// <summary>Краткое представление DMN-таблицы для списка.</summary>
public record DmnTableListItemDto(
    Guid Id,
    string Name,
    string? Description,
    DmnHitPolicy HitPolicy,
    int TotalVersions,
    DmnVersionStatus? LatestVersionStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Полное представление DMN-таблицы.</summary>
public record DmnTableDto(
    Guid Id,
    string Name,
    string? Description,
    DmnHitPolicy HitPolicy,
    int TotalVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Запрос на создание DMN-таблицы.</summary>
public record CreateDmnTableRequest(
    string Name,
    string? Description,
    DmnHitPolicy HitPolicy
);

/// <summary>Запрос на обновление метаданных DMN-таблицы.</summary>
public record UpdateDmnTableRequest(
    string Name,
    string? Description,
    DmnHitPolicy HitPolicy
);

// ─── Версия ──────────────────────────────────────────────────────────────────

/// <summary>Краткая информация о версии DMN-таблицы (без схемы).</summary>
public record DmnTableVersionInfoDto(
    Guid Id,
    int VersionNumber,
    DmnVersionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt
);

/// <summary>Полная схема версии DMN-таблицы (колонки + строки + ячейки).</summary>
public record DmnTableVersionDto(
    Guid Id,
    Guid TableId,
    int VersionNumber,
    DmnVersionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<DmnColumnDto> Columns,
    IReadOnlyList<DmnRowDto> Rows
);

// ─── Схема колонок и строк ────────────────────────────────────────────────────

/// <summary>DTO колонки DMN-таблицы.</summary>
public record DmnColumnDto(
    Guid Id,
    string Name,
    DmnColumnKind ColumnKind,
    DmnValueType ValueType,
    int Order
);

/// <summary>DTO строки DMN-таблицы (с ячейками).</summary>
public record DmnRowDto(
    Guid Id,
    int Order,
    IReadOnlyList<DmnCellDto> Cells
);

/// <summary>DTO ячейки DMN-таблицы.</summary>
public record DmnCellDto(
    Guid Id,
    Guid ColumnId,
    string? Value,
    string? Annotation
);

// ─── Сохранение черновика ─────────────────────────────────────────────────────

/// <summary>Запрос на сохранение нового черновика DMN-таблицы.</summary>
public record SaveDmnTableVersionRequest(
    IReadOnlyList<SaveDmnColumnRequest> Columns,
    IReadOnlyList<SaveDmnRowRequest> Rows
);

/// <summary>Колонка в запросе сохранения.</summary>
public record SaveDmnColumnRequest(
    /// <summary>Id существующей колонки (null — новая колонка).</summary>
    Guid? Id,
    string Name,
    DmnColumnKind ColumnKind,
    DmnValueType ValueType,
    int Order
);

/// <summary>Строка в запросе сохранения.</summary>
public record SaveDmnRowRequest(
    /// <summary>Id существующей строки (null — новая строка).</summary>
    Guid? Id,
    int Order,
    IReadOnlyList<SaveDmnCellRequest> Cells
);

/// <summary>Ячейка в запросе сохранения.</summary>
public record SaveDmnCellRequest(
    /// <summary>Ключ: id колонки (из SaveDmnColumnRequest.Id или нового Id).</summary>
    Guid? ColumnId,
    /// <summary>Индекс колонки в массиве Columns (используется если ColumnId = null).</summary>
    int? ColumnIndex,
    string? Value,
    string? Annotation
);

// ─── Тестирование ─────────────────────────────────────────────────────────────

/// <summary>Запрос на тестирование версии DMN-таблицы.</summary>
public record DmnTestRequest(
    /// <summary>Входные значения: ключ — Id входной колонки, значение — строковое представление.</summary>
    Dictionary<Guid, string?> Inputs
);

/// <summary>Результат тестирования.</summary>
public record DmnTestResponse(
    /// <summary>Применённая хит-политика.</summary>
    DmnHitPolicy HitPolicy,
    /// <summary>Список сработавших строк.</summary>
    IReadOnlyList<DmnMatchedRowDto> MatchedRows
);

/// <summary>Сработавшая строка.</summary>
public record DmnMatchedRowDto(
    Guid RowId,
    int RowOrder,
    /// <summary>Выходные значения: ключ — Id выходной колонки, значение — строка.</summary>
    Dictionary<Guid, string?> Outputs
);
