using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса конфигураций BPMN-элементов.</summary>
public class BpmElementConfigService : IBpmElementConfigService
{
    private readonly AppDbContext _db;

    public BpmElementConfigService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmElementConfigDto>> GetConfigsAsync(Guid processId, CancellationToken ct = default)
    {
        var configs = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == processId)
            .OrderBy(c => c.ElementId)
            .ToListAsync(ct);

        return configs.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmElementConfigDto?> GetConfigAsync(Guid processId, string elementId, CancellationToken ct = default)
    {
        var config = await _db.BpmElementConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProcessId == processId && c.ElementId == elementId, ct);

        return config is null ? null : MapToDto(config);
    }

    /// <inheritdoc />
    public async Task<BpmElementConfigDto> UpsertConfigAsync(Guid processId, string elementId, UpsertElementConfigRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ValidationException("Идентификатор элемента обязателен");

        // Проверяем существование процесса
        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        var config = await _db.BpmElementConfigs
            .FirstOrDefaultAsync(c => c.ProcessId == processId && c.ElementId == elementId, ct);

        if (config is null)
        {
            config = new BpmElementConfig
            {
                Id = Guid.NewGuid(),
                ProcessId = processId,
                ElementId = elementId,
                ConfigJson = request.ConfigJson,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _db.BpmElementConfigs.Add(config);
        }
        else
        {
            config.ConfigJson = request.ConfigJson;
            config.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return MapToDto(config);
    }

    /// <inheritdoc />
    public async Task DeleteConfigAsync(Guid processId, string elementId, CancellationToken ct = default)
    {
        var config = await _db.BpmElementConfigs
            .FirstOrDefaultAsync(c => c.ProcessId == processId && c.ElementId == elementId, ct)
            ?? throw new NotFoundException($"Конфигурация элемента {elementId} не найдена");

        _db.BpmElementConfigs.Remove(config);
        await _db.SaveChangesAsync(ct);
    }

    private static BpmElementConfigDto MapToDto(BpmElementConfig c) =>
        new(c.ElementId, c.ConfigJson, c.UpdatedAt);
}
