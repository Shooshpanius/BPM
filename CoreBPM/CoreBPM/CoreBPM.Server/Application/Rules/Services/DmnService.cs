using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Rules.DTOs;
using CoreBPM.Server.Application.Rules.Interfaces;
using CoreBPM.Server.Domain.Rules;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Rules.Services;

/// <summary>Реализация сервиса управления DMN-таблицами бизнес-правил.</summary>
public class DmnService : IDmnService
{
    private readonly AppDbContext _db;

    public DmnService(AppDbContext db)
    {
        _db = db;
    }

    // ─── CRUD таблиц ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<DmnTableListItemDto>> GetTablesAsync(CancellationToken ct = default)
    {
        var tables = await _db.DmnTables
            .AsNoTracking()
            .Include(t => t.Versions)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return tables.Select(t =>
        {
            var latest = t.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            return new DmnTableListItemDto(
                t.Id, t.Name, t.Description, t.HitPolicy,
                t.Versions.Count,
                latest?.Status,
                t.CreatedAt, t.UpdatedAt);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<DmnTableDto> GetTableByIdAsync(Guid tableId, CancellationToken ct = default)
    {
        var table = await _db.DmnTables
            .AsNoTracking()
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new NotFoundException($"DMN-таблица {tableId} не найдена");

        return MapToDto(table);
    }

    /// <inheritdoc />
    public async Task<DmnTableDto> CreateTableAsync(CreateDmnTableRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название таблицы обязательно");

        var now = DateTimeOffset.UtcNow;
        var table = new DmnTable
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            HitPolicy = request.HitPolicy,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Первый пустой черновик
        var draft = new DmnTableVersion
        {
            Id = Guid.NewGuid(),
            TableId = table.Id,
            VersionNumber = 1,
            Status = DmnVersionStatus.Draft,
            CreatedAt = now
        };
        table.Versions.Add(draft);

        _db.DmnTables.Add(table);
        await _db.SaveChangesAsync(ct);

        return await GetTableByIdAsync(table.Id, ct);
    }

    /// <inheritdoc />
    public async Task<DmnTableDto> UpdateTableAsync(Guid tableId, UpdateDmnTableRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название таблицы обязательно");

        var table = await _db.DmnTables.FindAsync(new object[] { tableId }, ct)
            ?? throw new NotFoundException($"DMN-таблица {tableId} не найдена");

        table.Name = request.Name.Trim();
        table.Description = request.Description?.Trim();
        table.HitPolicy = request.HitPolicy;
        table.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetTableByIdAsync(tableId, ct);
    }

    /// <inheritdoc />
    public async Task DeleteTableAsync(Guid tableId, CancellationToken ct = default)
    {
        var table = await _db.DmnTables
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new NotFoundException($"DMN-таблица {tableId} не найдена");

        var hasPublished = table.Versions.Any(v => v.Status == DmnVersionStatus.Published);
        if (hasPublished)
            throw new ValidationException("Нельзя удалить таблицу с опубликованными версиями");

        _db.DmnTables.Remove(table);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Версионирование ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<DmnTableVersionInfoDto>> GetVersionsAsync(Guid tableId, CancellationToken ct = default)
    {
        var tableExists = await _db.DmnTables.AnyAsync(t => t.Id == tableId, ct);
        if (!tableExists)
            throw new NotFoundException($"DMN-таблица {tableId} не найдена");

        return await _db.DmnTableVersions
            .AsNoTracking()
            .Where(v => v.TableId == tableId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DmnTableVersionInfoDto(v.Id, v.VersionNumber, v.Status, v.CreatedAt, v.PublishedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<DmnTableVersionDto> GetVersionAsync(Guid tableId, Guid versionId, CancellationToken ct = default)
    {
        var version = await _db.DmnTableVersions
            .AsNoTracking()
            .Include(v => v.Columns.OrderBy(c => c.Order))
            .Include(v => v.Rows.OrderBy(r => r.Order))
                .ThenInclude(r => r.Cells)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TableId == tableId, ct)
            ?? throw new NotFoundException($"Версия {versionId} DMN-таблицы {tableId} не найдена");

        return MapVersionToDto(version);
    }

    /// <inheritdoc />
    public async Task<DmnTableVersionDto> SaveDraftAsync(Guid tableId, SaveDmnTableVersionRequest request, CancellationToken ct = default)
    {
        var table = await _db.DmnTables
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new NotFoundException($"DMN-таблица {tableId} не найдена");

        var maxVersion = table.Versions.Any()
            ? table.Versions.Max(v => v.VersionNumber)
            : 0;

        var now = DateTimeOffset.UtcNow;
        var draft = new DmnTableVersion
        {
            Id = Guid.NewGuid(),
            TableId = tableId,
            VersionNumber = maxVersion + 1,
            Status = DmnVersionStatus.Draft,
            CreatedAt = now
        };

        // Строим маппинг индексов колонок → реальные Id
        var columnIdMap = new Dictionary<int, Guid>();
        for (var i = 0; i < request.Columns.Count; i++)
        {
            var colReq = request.Columns[i];
            var colId = colReq.Id ?? Guid.NewGuid();
            columnIdMap[i] = colId;

            draft.Columns.Add(new DmnColumn
            {
                Id = colId,
                VersionId = draft.Id,
                Name = colReq.Name,
                ColumnKind = colReq.ColumnKind,
                ValueType = colReq.ValueType,
                Order = colReq.Order
            });
        }

        foreach (var rowReq in request.Rows)
        {
            var row = new DmnRow
            {
                Id = rowReq.Id ?? Guid.NewGuid(),
                VersionId = draft.Id,
                Order = rowReq.Order
            };

            foreach (var cellReq in rowReq.Cells)
            {
                // Определяем Id колонки: по явному ColumnId или по ColumnIndex
                Guid columnId;
                if (cellReq.ColumnId.HasValue)
                    columnId = cellReq.ColumnId.Value;
                else if (cellReq.ColumnIndex.HasValue && columnIdMap.TryGetValue(cellReq.ColumnIndex.Value, out var mappedId))
                    columnId = mappedId;
                else
                    throw new ValidationException("Ячейка не содержит корректный ColumnId или ColumnIndex");

                row.Cells.Add(new DmnCell
                {
                    Id = Guid.NewGuid(),
                    RowId = row.Id,
                    ColumnId = columnId,
                    Value = cellReq.Value,
                    Annotation = cellReq.Annotation
                });
            }

            draft.Rows.Add(row);
        }

        _db.DmnTableVersions.Add(draft);

        // Обновляем дату изменения таблицы
        table.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return await GetVersionAsync(tableId, draft.Id, ct);
    }

    /// <inheritdoc />
    public async Task<DmnTableVersionInfoDto> PublishVersionAsync(Guid tableId, Guid versionId, CancellationToken ct = default)
    {
        var version = await _db.DmnTableVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TableId == tableId, ct)
            ?? throw new NotFoundException($"Версия {versionId} DMN-таблицы {tableId} не найдена");

        if (version.Status == DmnVersionStatus.Published)
            throw new ValidationException("Версия уже опубликована");

        // Архивируем текущую опубликованную версию
        var currentPublished = await _db.DmnTableVersions
            .Where(v => v.TableId == tableId && v.Status == DmnVersionStatus.Published)
            .ToListAsync(ct);

        foreach (var prev in currentPublished)
            prev.Status = DmnVersionStatus.Archived;

        var now = DateTimeOffset.UtcNow;
        version.Status = DmnVersionStatus.Published;
        version.PublishedAt = now;

        // Обновляем дату изменения таблицы
        var table = await _db.DmnTables.FindAsync(new object[] { tableId }, ct);
        if (table != null)
            table.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return new DmnTableVersionInfoDto(version.Id, version.VersionNumber, version.Status, version.CreatedAt, version.PublishedAt);
    }

    // ─── Тестирование ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DmnTestResponse> EvaluateAsync(Guid tableId, Guid versionId, DmnTestRequest request, CancellationToken ct = default)
    {
        var table = await _db.DmnTables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tableId, ct)
            ?? throw new NotFoundException($"DMN-таблица {tableId} не найдена");

        var version = await _db.DmnTableVersions
            .AsNoTracking()
            .Include(v => v.Columns.OrderBy(c => c.Order))
            .Include(v => v.Rows.OrderBy(r => r.Order))
                .ThenInclude(r => r.Cells)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.TableId == tableId, ct)
            ?? throw new NotFoundException($"Версия {versionId} DMN-таблицы {tableId} не найдена");

        var inputColumns = version.Columns.Where(c => c.ColumnKind == DmnColumnKind.Input).ToList();
        var outputColumns = version.Columns.Where(c => c.ColumnKind == DmnColumnKind.Output).ToList();

        var matched = new List<DmnMatchedRowDto>();

        foreach (var row in version.Rows)
        {
            if (RowMatches(row, inputColumns, request.Inputs))
            {
                var outputs = outputColumns.ToDictionary(
                    col => col.Id,
                    col => row.Cells.FirstOrDefault(c => c.ColumnId == col.Id)?.Value);
                matched.Add(new DmnMatchedRowDto(row.Id, row.Order, outputs));
            }
        }

        // Применяем хит-политику
        var result = ApplyHitPolicy(table.HitPolicy, matched, version.Rows.Count);

        return new DmnTestResponse(table.HitPolicy, result);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static bool RowMatches(DmnRow row, List<DmnColumn> inputColumns, Dictionary<Guid, string?> inputs)
    {
        foreach (var col in inputColumns)
        {
            var cell = row.Cells.FirstOrDefault(c => c.ColumnId == col.Id);
            var cellValue = cell?.Value;

            // Пустая ячейка — совпадает всегда
            if (string.IsNullOrWhiteSpace(cellValue))
                continue;

            inputs.TryGetValue(col.Id, out var inputValue);

            if (!CellMatches(cellValue, inputValue, col.ValueType))
                return false;
        }
        return true;
    }

    private static bool CellMatches(string cellValue, string? inputValue, DmnValueType valueType)
    {
        var cv = cellValue.Trim();

        // Список значений "a","b","c"
        if (cv.StartsWith('"') && cv.Contains(','))
        {
            var items = cv.Split(',')
                .Select(s => s.Trim().Trim('"'))
                .ToList();
            var iv = inputValue?.Trim().Trim('"') ?? string.Empty;
            return items.Contains(iv, StringComparer.OrdinalIgnoreCase);
        }

        // Числовые и датовые сравнения
        if (valueType == DmnValueType.Number || valueType == DmnValueType.Date)
        {
            // Диапазон [a..b]
            if (cv.StartsWith('[') && cv.EndsWith(']') && cv.Contains(".."))
            {
                var inner = cv[1..^1];
                var parts = inner.Split("..");
                if (parts.Length == 2
                    && double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lo)
                    && double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hi)
                    && double.TryParse(inputValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inp))
                    return inp >= lo && inp <= hi;
                return false;
            }

            // Операторы сравнения: <=, >=, <, >
            if (cv.Length > 1 && (cv.StartsWith("<=") || cv.StartsWith(">=")))
            {
                var op = cv[..2];
                var numStr = cv[2..].Trim();
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var threshold)
                    && double.TryParse(inputValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inp))
                {
                    return op == "<=" ? inp <= threshold : inp >= threshold;
                }
                return false;
            }
            if (cv.StartsWith('<') || cv.StartsWith('>'))
            {
                var op = cv[0];
                var numStr = cv[1..].Trim();
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var threshold)
                    && double.TryParse(inputValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var inp))
                {
                    return op == '<' ? inp < threshold : inp > threshold;
                }
                return false;
            }
        }

        // Булево
        if (valueType == DmnValueType.Boolean)
        {
            if (bool.TryParse(cv, out var boolCell) && bool.TryParse(inputValue, out var boolInput))
                return boolCell == boolInput;
            return string.Equals(cv, inputValue, StringComparison.OrdinalIgnoreCase);
        }

        // Строка — точное совпадение (без учёта регистра)
        return string.Equals(cv.Trim('"'), (inputValue ?? string.Empty).Trim('"'), StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DmnMatchedRowDto> ApplyHitPolicy(DmnHitPolicy policy, List<DmnMatchedRowDto> matched, int totalRows)
    {
        return policy switch
        {
            DmnHitPolicy.Unique when matched.Count > 1
                => throw new ValidationException($"Хит-политика UNIQUE нарушена: совпало {matched.Count} строк вместо одной"),
            DmnHitPolicy.Unique => matched,
            DmnHitPolicy.First => matched.Take(1).ToList(),
            DmnHitPolicy.Any => matched, // Все должны давать одинаковый результат — проверка опциональна
            DmnHitPolicy.Collect => matched,
            DmnHitPolicy.RuleOrder => matched.OrderBy(r => r.RowOrder).ToList(),
            DmnHitPolicy.OutputOrder => matched.OrderBy(r =>
            {
                var firstOutput = r.Outputs.Values.FirstOrDefault();
                return firstOutput ?? string.Empty;
            }).ToList(),
            _ => matched
        };
    }

    // ─── Маппинг ──────────────────────────────────────────────────────────────

    private static DmnTableDto MapToDto(DmnTable t) =>
        new(t.Id, t.Name, t.Description, t.HitPolicy, t.Versions.Count, t.CreatedAt, t.UpdatedAt);

    private static DmnTableVersionDto MapVersionToDto(DmnTableVersion v)
    {
        var columns = v.Columns.OrderBy(c => c.Order).Select(c =>
            new DmnColumnDto(c.Id, c.Name, c.ColumnKind, c.ValueType, c.Order)).ToList();

        var rows = v.Rows.OrderBy(r => r.Order).Select(r =>
        {
            var cells = r.Cells.Select(c =>
                new DmnCellDto(c.Id, c.ColumnId, c.Value, c.Annotation)).ToList();
            return new DmnRowDto(r.Id, r.Order, cells);
        }).ToList();

        return new DmnTableVersionDto(v.Id, v.TableId, v.VersionNumber, v.Status, v.CreatedAt, v.PublishedAt, columns, rows);
    }
}
