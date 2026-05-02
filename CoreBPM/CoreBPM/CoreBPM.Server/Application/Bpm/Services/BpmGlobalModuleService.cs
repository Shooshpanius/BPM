using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления глобальными C#-модулями (FR-BPM-01.7).</summary>
public class BpmGlobalModuleService : IBpmGlobalModuleService
{
    private readonly AppDbContext _db;

    public BpmGlobalModuleService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmGlobalModuleDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var modules = await _db.BpmGlobalModules
            .AsNoTracking()
            .Include(m => m.Files)
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return modules.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmGlobalModuleDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var module = await _db.BpmGlobalModules
            .AsNoTracking()
            .Include(m => m.Files)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"Глобальный модуль {id} не найден");

        return MapToDto(module);
    }

    /// <inheritdoc />
    public async Task<BpmGlobalModuleDto> CreateAsync(CreateGlobalModuleRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название модуля обязательно");

        var module = new BpmGlobalModule
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsPublished = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.BpmGlobalModules.Add(module);
        await _db.SaveChangesAsync(ct);
        return MapToDto(module);
    }

    /// <inheritdoc />
    public async Task<BpmGlobalModuleDto> UpdateAsync(Guid id, UpdateGlobalModuleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название модуля обязательно");

        var module = await _db.BpmGlobalModules
            .Include(m => m.Files)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"Глобальный модуль {id} не найден");

        module.Name = request.Name.Trim();
        module.Description = request.Description?.Trim();
        module.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(module);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var module = await _db.BpmGlobalModules
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"Глобальный модуль {id} не найден");

        module.IsDeleted = true;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BpmGlobalModuleDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var module = await _db.BpmGlobalModules
            .Include(m => m.Files)
            .FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new NotFoundException($"Глобальный модуль {id} не найден");

        module.IsPublished = true;
        module.PublishedAt = DateTimeOffset.UtcNow;
        module.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToDto(module);
    }

    // ─── Файлы модуля ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmGlobalModuleFileDto>> ListFilesAsync(Guid moduleId, CancellationToken ct = default)
    {
        await EnsureModuleExistsAsync(moduleId, ct);

        var files = await _db.BpmGlobalModuleFiles
            .AsNoTracking()
            .Where(f => f.ModuleId == moduleId)
            .OrderBy(f => f.Order)
            .ThenBy(f => f.FileName)
            .ToListAsync(ct);

        return files.Select(MapFileToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmGlobalModuleFileDto> AddFileAsync(Guid moduleId, CreateGlobalModuleFileRequest request, CancellationToken ct = default)
    {
        await EnsureModuleExistsAsync(moduleId, ct);

        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ValidationException("Имя файла обязательно");

        var maxOrder = await _db.BpmGlobalModuleFiles
            .Where(f => f.ModuleId == moduleId)
            .Select(f => (int?)f.Order)
            .MaxAsync(ct) ?? -1;

        var file = new BpmGlobalModuleFile
        {
            Id = Guid.NewGuid(),
            ModuleId = moduleId,
            FileName = request.FileName.Trim(),
            ScriptBody = request.ScriptBody ?? string.Empty,
            Order = maxOrder + 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.BpmGlobalModuleFiles.Add(file);

        // Снять флаг публикации модуля при добавлении файла
        var module = await _db.BpmGlobalModules.FindAsync(new object[] { moduleId }, ct);
        if (module is not null) module.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapFileToDto(file);
    }

    /// <inheritdoc />
    public async Task<BpmGlobalModuleFileDto> UpdateFileAsync(Guid moduleId, Guid fileId, UpdateGlobalModuleFileRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ValidationException("Имя файла обязательно");

        var file = await _db.BpmGlobalModuleFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ModuleId == moduleId, ct)
            ?? throw new NotFoundException($"Файл {fileId} не найден в модуле {moduleId}");

        file.FileName = request.FileName.Trim();
        file.ScriptBody = request.ScriptBody ?? string.Empty;
        file.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapFileToDto(file);
    }

    /// <inheritdoc />
    public async Task DeleteFileAsync(Guid moduleId, Guid fileId, CancellationToken ct = default)
    {
        var file = await _db.BpmGlobalModuleFiles
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ModuleId == moduleId, ct)
            ?? throw new NotFoundException($"Файл {fileId} не найден в модуле {moduleId}");

        _db.BpmGlobalModuleFiles.Remove(file);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReorderFilesAsync(Guid moduleId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        var files = await _db.BpmGlobalModuleFiles
            .Where(f => f.ModuleId == moduleId)
            .ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var file = files.FirstOrDefault(f => f.Id == orderedIds[i]);
            if (file is not null)
            {
                file.Order = i;
                file.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── Вспомогательные методы ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<byte[]> ExportAsync(Guid organizationId, CancellationToken ct = default)
    {
        var modules = await _db.BpmGlobalModules
            .AsNoTracking()
            .Include(m => m.Files)
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        var exportList = modules.Select(m => new
        {
            name = m.Name,
            description = m.Description,
            files = m.Files.OrderBy(f => f.Order).Select(f => new
            {
                fileName = f.FileName,
                scriptBody = f.ScriptBody,
                order = f.Order
            }).ToList()
        }).ToList();

        return JsonSerializer.SerializeToUtf8Bytes(exportList);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmGlobalModuleDto>> ImportAsync(Guid organizationId, byte[] jsonData, Guid createdByUserId, CancellationToken ct = default)
    {
        const string DefaultModuleFileName = "module.cs";

        List<JsonElement> elements;
        try
        {
            elements = JsonSerializer.Deserialize<List<JsonElement>>(jsonData)
                ?? throw new ValidationException("Пустой список модулей");
        }
        catch (JsonException ex) { throw new ValidationException($"Невалидный JSON: {ex.Message}"); }

        var result = new List<BpmGlobalModuleDto>();
        foreach (var el in elements)
        {
            var name = el.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty;
            var description = el.TryGetProperty("description", out var descProp) ? descProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(name)) continue;

            var existing = await _db.BpmGlobalModules
                .Include(m => m.Files)
                .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.Name == name, ct);

            Guid moduleId;
            if (existing is not null)
            {
                existing.Description = description;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                moduleId = existing.Id;
            }
            else
            {
                var dto = await CreateAsync(new CreateGlobalModuleRequest(organizationId, name, description), createdByUserId, ct);
                moduleId = dto.Id;
            }

            // Импортируем файлы если есть
            if (el.TryGetProperty("files", out var filesProp) && filesProp.ValueKind == JsonValueKind.Array)
            {
                var module = await _db.BpmGlobalModules.Include(m => m.Files).FirstAsync(m => m.Id == moduleId, ct);
                // Удаляем старые файлы
                _db.BpmGlobalModuleFiles.RemoveRange(module.Files);
                await _db.SaveChangesAsync(ct);

                var order = 0;
                foreach (var fileEl in filesProp.EnumerateArray())
                {
                    var fileName = fileEl.TryGetProperty("fileName", out var fnProp) ? fnProp.GetString() ?? DefaultModuleFileName : DefaultModuleFileName;
                    var scriptBody = fileEl.TryGetProperty("scriptBody", out var sbProp) ? sbProp.GetString() ?? string.Empty : string.Empty;
                    var file = new BpmGlobalModuleFile
                    {
                        Id = Guid.NewGuid(),
                        ModuleId = moduleId,
                        FileName = fileName,
                        ScriptBody = scriptBody,
                        Order = order++,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _db.BpmGlobalModuleFiles.Add(file);
                }
                await _db.SaveChangesAsync(ct);
            }

            result.Add(await GetAsync(moduleId, ct));
        }
        return result;
    }

    private async Task EnsureModuleExistsAsync(Guid moduleId, CancellationToken ct)
    {
        var exists = await _db.BpmGlobalModules.AnyAsync(m => m.Id == moduleId, ct);
        if (!exists)
            throw new NotFoundException($"Глобальный модуль {moduleId} не найден");
    }

    private static BpmGlobalModuleDto MapToDto(BpmGlobalModule m) =>
        new(m.Id, m.OrganizationId, m.Name, m.Description, m.IsPublished, m.Files.Count, m.CreatedAt, m.UpdatedAt, m.PublishedAt);

    private static BpmGlobalModuleFileDto MapFileToDto(BpmGlobalModuleFile f) =>
        new(f.Id, f.ModuleId, f.FileName, f.ScriptBody, f.Order, f.UpdatedAt);
}
