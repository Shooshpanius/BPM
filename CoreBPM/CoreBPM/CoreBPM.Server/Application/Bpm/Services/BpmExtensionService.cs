using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления пользовательскими расширениями палитры дизайнера (FR-BPM-01.7).</summary>
public class BpmExtensionService : IBpmExtensionService
{
    private readonly AppDbContext _db;

    public BpmExtensionService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmDesignerExtensionDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        var items = await _db.BpmDesignerExtensions
            .AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .OrderBy(e => e.FolderPath)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

        return items.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmDesignerExtensionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var ext = await _db.BpmDesignerExtensions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Расширение {id} не найдено");

        return MapToDto(ext);
    }

    /// <inheritdoc />
    public async Task<BpmDesignerExtensionDto> CreateAsync(CreateDesignerExtensionRequest request, Guid createdByUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название расширения обязательно");

        var ext = new BpmDesignerExtension
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            FolderPath = NormalizeFolderPath(request.FolderPath),
            ScriptBody = request.ScriptBody ?? string.Empty,
            IsPublished = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.BpmDesignerExtensions.Add(ext);
        await _db.SaveChangesAsync(ct);
        return MapToDto(ext);
    }

    /// <inheritdoc />
    public async Task<BpmDesignerExtensionDto> UpdateAsync(Guid id, UpdateDesignerExtensionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название расширения обязательно");

        var ext = await _db.BpmDesignerExtensions
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Расширение {id} не найдено");

        ext.Name = request.Name.Trim();
        ext.Description = request.Description?.Trim();
        ext.FolderPath = NormalizeFolderPath(request.FolderPath);
        ext.ScriptBody = request.ScriptBody ?? string.Empty;
        ext.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(ext);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var ext = await _db.BpmDesignerExtensions
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Расширение {id} не найдено");

        ext.IsDeleted = true;
        ext.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BpmDesignerExtensionDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var ext = await _db.BpmDesignerExtensions
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Расширение {id} не найдено");

        ext.IsPublished = true;
        ext.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return MapToDto(ext);
    }

    /// <inheritdoc />
    public async Task<BpmDesignerExtensionDto> CopyAsync(Guid id, Guid createdByUserId, CancellationToken ct = default)
    {
        var source = await _db.BpmDesignerExtensions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NotFoundException($"Расширение {id} не найдено");

        var copy = new BpmDesignerExtension
        {
            Id = Guid.NewGuid(),
            OrganizationId = source.OrganizationId,
            Name = $"{source.Name} (копия)",
            Description = source.Description,
            FolderPath = source.FolderPath,
            ScriptBody = source.ScriptBody,
            IsPublished = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.BpmDesignerExtensions.Add(copy);
        await _db.SaveChangesAsync(ct);
        return MapToDto(copy);
    }

    // ─── Вспомогательные методы ─────────────────────────────────────────────

    private static string? NormalizeFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return path.Trim().Trim('/');
    }

    private static BpmDesignerExtensionDto MapToDto(BpmDesignerExtension e) =>
        new(e.Id, e.OrganizationId, e.Name, e.Description, e.FolderPath, e.ScriptBody, e.IsPublished, e.CreatedByUserId, e.CreatedAt, e.UpdatedAt);
}
