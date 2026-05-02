using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления формами задач (FR-BPM-01.4).</summary>
public class FormService : IFormService
{
    private readonly AppDbContext _db;

    public FormService(AppDbContext db)
    {
        _db = db;
    }

    // ─── CRUD форм ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<FormListItemDto>> GetFormsAsync(Guid? processId, CancellationToken ct = default)
    {
        var query = _db.BpmTaskForms
            .AsNoTracking()
            .Include(f => f.Versions)
            .AsQueryable();

        if (processId.HasValue)
            query = query.Where(f => f.ProcessId == processId.Value);

        var forms = await query.OrderBy(f => f.Name).ToListAsync(ct);

        return forms.Select(f =>
        {
            var latest = f.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
            return new FormListItemDto(
                f.Id, f.Name, f.Description, f.ProcessId, f.ElementId,
                f.Versions.Count,
                latest?.Status,
                f.CreatedAt, f.UpdatedAt);
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<FormDto> GetFormByIdAsync(Guid formId, CancellationToken ct = default)
    {
        var form = await _db.BpmTaskForms
            .AsNoTracking()
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        return MapToDto(form);
    }

    /// <inheritdoc />
    public async Task<FormDto> CreateFormAsync(CreateFormRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название формы обязательно");

        var now = DateTimeOffset.UtcNow;
        var form = new BpmTaskForm
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            ProcessId = request.ProcessId,
            ElementId = request.ElementId?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        // Создаём пустой черновик версии 1
        var draft = new BpmTaskFormVersion
        {
            Id = Guid.NewGuid(),
            FormId = form.Id,
            VersionNumber = 1,
            Schema = "{}",
            Status = BpmFormVersionStatus.Draft,
            CreatedAt = now
        };
        form.Versions.Add(draft);

        _db.BpmTaskForms.Add(form);
        await _db.SaveChangesAsync(ct);

        return await GetFormByIdAsync(form.Id, ct);
    }

    /// <inheritdoc />
    public async Task<FormDto> UpdateFormAsync(Guid formId, UpdateFormRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название формы обязательно");

        var form = await _db.BpmTaskForms.FindAsync(new object[] { formId }, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        form.Name = request.Name.Trim();
        form.Description = request.Description?.Trim();
        form.ProcessId = request.ProcessId;
        form.ElementId = request.ElementId?.Trim();
        form.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetFormByIdAsync(formId, ct);
    }

    /// <inheritdoc />
    public async Task DeleteFormAsync(Guid formId, CancellationToken ct = default)
    {
        var form = await _db.BpmTaskForms
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        var hasPublished = form.Versions.Any(v => v.Status == BpmFormVersionStatus.Published);
        if (hasPublished)
            throw new ValidationException("Нельзя удалить форму с опубликованными версиями");

        _db.BpmTaskForms.Remove(form);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Версионирование ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<FormVersionInfoDto>> GetVersionsAsync(Guid formId, CancellationToken ct = default)
    {
        var exists = await _db.BpmTaskForms.AnyAsync(f => f.Id == formId, ct);
        if (!exists)
            throw new NotFoundException($"Форма {formId} не найдена");

        return await _db.BpmTaskFormVersions
            .AsNoTracking()
            .Where(v => v.FormId == formId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new FormVersionInfoDto(v.Id, v.VersionNumber, v.Status, v.CreatedAt, v.PublishedAt))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<FormVersionDto> GetVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default)
    {
        var version = await _db.BpmTaskFormVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FormId == formId, ct)
            ?? throw new NotFoundException($"Версия {versionId} формы {formId} не найдена");

        return MapVersionToDto(version);
    }

    /// <inheritdoc />
    public async Task<FormVersionDto> SaveDraftAsync(Guid formId, SaveFormVersionRequest request, CancellationToken ct = default)
    {
        var form = await _db.BpmTaskForms
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        var maxVersion = form.Versions.Any()
            ? form.Versions.Max(v => v.VersionNumber)
            : 0;

        // Сериализуем схему в JSON-строку
        var schemaJson = request.Schema is string s
            ? s
            : JsonSerializer.Serialize(request.Schema);

        var now = DateTimeOffset.UtcNow;
        var draft = new BpmTaskFormVersion
        {
            Id = Guid.NewGuid(),
            FormId = formId,
            VersionNumber = maxVersion + 1,
            Schema = schemaJson,
            Status = BpmFormVersionStatus.Draft,
            CreatedAt = now
        };

        _db.BpmTaskFormVersions.Add(draft);
        form.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return await GetVersionAsync(formId, draft.Id, ct);
    }

    /// <inheritdoc />
    public async Task<FormVersionInfoDto> PublishVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default)
    {
        var version = await _db.BpmTaskFormVersions
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FormId == formId, ct)
            ?? throw new NotFoundException($"Версия {versionId} формы {formId} не найдена");

        if (version.Status == BpmFormVersionStatus.Published)
            throw new ValidationException("Версия уже опубликована");

        // Архивируем текущие опубликованные версии
        var currentPublished = await _db.BpmTaskFormVersions
            .Where(v => v.FormId == formId && v.Status == BpmFormVersionStatus.Published)
            .ToListAsync(ct);

        foreach (var prev in currentPublished)
            prev.Status = BpmFormVersionStatus.Archived;

        var now = DateTimeOffset.UtcNow;
        version.Status = BpmFormVersionStatus.Published;
        version.PublishedAt = now;

        var form = await _db.BpmTaskForms.FindAsync(new object[] { formId }, ct);
        if (form != null)
            form.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return new FormVersionInfoDto(version.Id, version.VersionNumber, version.Status, version.CreatedAt, version.PublishedAt);
    }

    /// <inheritdoc />
    public async Task<FormVersionDto> RollbackVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default)
    {
        var source = await _db.BpmTaskFormVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FormId == formId, ct)
            ?? throw new NotFoundException($"Версия {versionId} формы {formId} не найдена");

        var form = await _db.BpmTaskForms
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        var maxVersion = form.Versions.Max(v => v.VersionNumber);
        var now = DateTimeOffset.UtcNow;

        // Создаём новую версию-копию
        var rollback = new BpmTaskFormVersion
        {
            Id = Guid.NewGuid(),
            FormId = formId,
            VersionNumber = maxVersion + 1,
            Schema = source.Schema,
            Status = BpmFormVersionStatus.Draft,
            CreatedAt = now
        };

        _db.BpmTaskFormVersions.Add(rollback);
        form.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        return await GetVersionAsync(formId, rollback.Id, ct);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportVersionAsync(Guid formId, Guid versionId, CancellationToken ct = default)
    {
        var form = await _db.BpmTaskForms
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        var version = await _db.BpmTaskFormVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FormId == formId, ct)
            ?? throw new NotFoundException($"Версия {versionId} формы {formId} не найдена");

        var exportObj = new
        {
            formId = form.Id,
            formName = form.Name,
            versionNumber = version.VersionNumber,
            status = version.Status.ToString(),
            schema = version.Schema,
            exportedAt = DateTimeOffset.UtcNow
        };

        return JsonSerializer.SerializeToUtf8Bytes(exportObj);
    }

    /// <inheritdoc />
    public async Task<FormVersionDto> ImportVersionAsync(Guid formId, byte[] jsonData, CancellationToken ct = default)
    {
        var form = await _db.BpmTaskForms
            .Include(f => f.Versions)
            .FirstOrDefaultAsync(f => f.Id == formId, ct)
            ?? throw new NotFoundException($"Форма {formId} не найдена");

        // Парсим JSON
        JsonElement root;
        try { root = JsonDocument.Parse(jsonData).RootElement; }
        catch (Exception ex) { throw new ValidationException($"Невалидный JSON: {ex.Message}"); }

        // Извлекаем схему
        var schemaJson = root.TryGetProperty("schema", out var schemaProp)
            ? schemaProp.GetRawText()
            : "{}";

        var maxVersion = form.Versions.Any() ? form.Versions.Max(v => v.VersionNumber) : 0;
        var now = DateTimeOffset.UtcNow;
        var draft = new BpmTaskFormVersion
        {
            Id = Guid.NewGuid(),
            FormId = formId,
            VersionNumber = maxVersion + 1,
            Schema = schemaJson,
            Status = BpmFormVersionStatus.Draft,
            CreatedAt = now
        };

        _db.BpmTaskFormVersions.Add(draft);
        form.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return await GetVersionAsync(formId, draft.Id, ct);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static FormDto MapToDto(BpmTaskForm f) =>
        new(f.Id, f.Name, f.Description, f.ProcessId, f.ElementId, f.Versions.Count, f.CreatedAt, f.UpdatedAt);

    private static FormVersionDto MapVersionToDto(BpmTaskFormVersion v)
    {
        object schema;
        try
        {
            schema = JsonSerializer.Deserialize<JsonElement>(v.Schema);
        }
        catch
        {
            schema = v.Schema;
        }
        return new FormVersionDto(v.Id, v.FormId, v.VersionNumber, v.Status, v.CreatedAt, v.PublishedAt, schema);
    }
}
