using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoreBPM.Server.Application.Tasks.Services;

/// <summary>Интерфейс сервиса системных настроек контроля и трудозатрат (FR-TASK-01.4).</summary>
public interface ITaskControlSettingsService
{
    /// <summary>Получить текущие настройки.</summary>
    Task<TaskControlSettingsDto> GetAsync(CancellationToken ct = default);

    /// <summary>Обновить настройки (Admin).</summary>
    Task<TaskControlSettingsDto> UpdateAsync(UpdateTaskControlSettingsRequest req, CancellationToken ct = default);
}

/// <inheritdoc />
public class TaskControlSettingsService : ITaskControlSettingsService
{
    private readonly AppDbContext _db;

    public TaskControlSettingsService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<TaskControlSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateAsync(ct);
        return ToDto(settings);
    }

    /// <inheritdoc />
    public async Task<TaskControlSettingsDto> UpdateAsync(UpdateTaskControlSettingsRequest req, CancellationToken ct = default)
    {
        var settings = await GetOrCreateAsync(ct);

        if (Enum.TryParse<TaskControlType>(req.DefaultControlType, true, out var parsedType))
            settings.DefaultControlType = parsedType;

        settings.IsEffortRequired = req.IsEffortRequired;
        settings.IsActivityTypeRequired = req.IsActivityTypeRequired;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private async Task<TaskControlSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var settings = await _db.TaskControlSettings.FirstOrDefaultAsync(s => s.Id == 1, ct);
        if (settings is null)
        {
            settings = new TaskControlSettings { Id = 1, UpdatedAt = DateTimeOffset.UtcNow };
            _db.TaskControlSettings.Add(settings);
            await _db.SaveChangesAsync(ct);
        }
        return settings;
    }

    private static TaskControlSettingsDto ToDto(TaskControlSettings s) => new()
    {
        DefaultControlType = s.DefaultControlType.ToString(),
        IsEffortRequired = s.IsEffortRequired,
        IsActivityTypeRequired = s.IsActivityTypeRequired,
        UpdatedAt = s.UpdatedAt,
    };
}
