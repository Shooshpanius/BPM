using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления C#-сценариями версий процессов (FR-BPM-01.7).</summary>
public class BpmScriptService : IBpmScriptService
{
    private readonly AppDbContext _db;

    public BpmScriptService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<BpmScriptModuleDto> GetScriptAsync(Guid processId, Guid versionId, CancellationToken ct = default)
    {
        await EnsureVersionBelongsToProcessAsync(processId, versionId, ct);

        var module = await _db.BpmScriptModules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProcessVersionId == versionId, ct);

        if (module is null)
        {
            // Возвращаем пустой DTO без сохранения в БД
            return new BpmScriptModuleDto(
                Id: Guid.Empty,
                ProcessVersionId: versionId,
                ScriptBody: string.Empty,
                Language: "CSharp",
                UpdatedAt: DateTimeOffset.UtcNow,
                PublishedAt: null
            );
        }

        return MapToDto(module);
    }

    /// <inheritdoc />
    public async Task<BpmScriptModuleDto> SaveScriptAsync(Guid processId, Guid versionId, SaveScriptModuleRequest request, CancellationToken ct = default)
    {
        await EnsureVersionBelongsToProcessAsync(processId, versionId, ct);

        var module = await _db.BpmScriptModules
            .FirstOrDefaultAsync(s => s.ProcessVersionId == versionId, ct);

        if (module is null)
        {
            module = new BpmScriptModule
            {
                Id = Guid.NewGuid(),
                ProcessVersionId = versionId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _db.BpmScriptModules.Add(module);
        }

        module.ScriptBody = request.ScriptBody ?? string.Empty;
        module.Language = string.IsNullOrWhiteSpace(request.Language) ? "CSharp" : request.Language.Trim();
        module.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(module);
    }

    /// <inheritdoc />
    public async Task<BpmScriptModuleDto> PublishScriptAsync(Guid processId, Guid versionId, CancellationToken ct = default)
    {
        await EnsureVersionBelongsToProcessAsync(processId, versionId, ct);

        var module = await _db.BpmScriptModules
            .FirstOrDefaultAsync(s => s.ProcessVersionId == versionId, ct)
            ?? throw new NotFoundException($"Сценарий для версии {versionId} не найден. Сохраните сценарий перед публикацией.");

        module.PublishedAt = DateTimeOffset.UtcNow;
        module.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(module);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessVersionScriptInfoDto>> ListProcessVersionScriptsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var versions = await _db.BpmProcessVersions
            .AsNoTracking()
            .Include(v => v.Process)
            .Include(v => v.ScriptModule)
            .Where(v => v.Process.OrganizationId == organizationId && !v.Process.IsDeleted)
            .OrderBy(v => v.Process.Name)
            .ThenByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

        return versions.Select(v => new BpmProcessVersionScriptInfoDto(
            ProcessId: v.ProcessId,
            ProcessName: v.Process.Name,
            VersionId: v.Id,
            VersionNumber: v.VersionNumber,
            VersionStatus: v.Status,
            HasScript: v.ScriptModule is not null && !string.IsNullOrEmpty(v.ScriptModule.ScriptBody),
            ScriptPublishedAt: v.ScriptModule?.PublishedAt
        )).ToList();
    }

    // ─── Вспомогательные методы ─────────────────────────────────────────────

    private async Task EnsureVersionBelongsToProcessAsync(Guid processId, Guid versionId, CancellationToken ct)
    {
        var exists = await _db.BpmProcessVersions
            .AnyAsync(v => v.Id == versionId && v.ProcessId == processId, ct);
        if (!exists)
            throw new NotFoundException($"Версия {versionId} не найдена в процессе {processId}");
    }

    private static BpmScriptModuleDto MapToDto(BpmScriptModule m) =>
        new(m.Id, m.ProcessVersionId, m.ScriptBody, m.Language, m.UpdatedAt, m.PublishedAt);
}
