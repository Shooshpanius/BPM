using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса RACI-матрицы ответственности бизнес-процесса.</summary>
public class BpmRaciService : IBpmRaciService
{
    private readonly AppDbContext _db;

    public BpmRaciService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmRaciEntryDto>> GetEntriesAsync(Guid processId, CancellationToken ct = default)
    {
        var entries = await _db.BpmRaciEntries
            .AsNoTracking()
            .Where(r => r.ProcessId == processId)
            .OrderBy(r => r.Stage)
            .ThenBy(r => r.Role)
            .ToListAsync(ct);

        return entries.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmRaciEntryDto>> ReplaceEntriesAsync(Guid processId, IReadOnlyList<UpsertRaciEntryRequest> entries, CancellationToken ct = default)
    {
        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        // Удаляем все существующие записи матрицы
        var existing = await _db.BpmRaciEntries
            .Where(r => r.ProcessId == processId)
            .ToListAsync(ct);
        _db.BpmRaciEntries.RemoveRange(existing);

        // Записываем новые
        var newEntries = entries.Select(r => new BpmRaciEntry
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            Stage = r.Stage.Trim(),
            Role = r.Role.Trim(),
            RaciType = r.RaciType,
        }).ToList();

        _db.BpmRaciEntries.AddRange(newEntries);
        await _db.SaveChangesAsync(ct);

        return newEntries.Select(MapToDto).ToList();
    }

    private static BpmRaciEntryDto MapToDto(BpmRaciEntry r) =>
        new(r.Id, r.Stage, r.Role, r.RaciType);
}
