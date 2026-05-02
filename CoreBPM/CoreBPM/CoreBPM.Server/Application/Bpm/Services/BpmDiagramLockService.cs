using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса мягких блокировок BPMN-диаграмм.</summary>
public class BpmDiagramLockService : IBpmDiagramLockService
{
    /// <summary>Блокировка действует 90 секунд (heartbeat каждые 30 с, запас × 3).</summary>
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(90);

    private readonly AppDbContext _db;

    public BpmDiagramLockService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<DiagramLockDto?> GetLockAsync(Guid processId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var @lock = await _db.BpmDiagramLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ProcessId == processId && l.LockedUntil > now, ct);

        return @lock is null ? null : MapToDto(@lock);
    }

    /// <inheritdoc />
    public async Task<AcquireLockResponse> AcquireAsync(
        Guid processId, Guid userId, string userDisplayName, CancellationToken ct = default)
    {
        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        var now = DateTimeOffset.UtcNow;
        var existing = await _db.BpmDiagramLocks
            .FirstOrDefaultAsync(l => l.ProcessId == processId, ct);

        if (existing is not null)
        {
            // Блокировка принадлежит текущему пользователю — продлеваем
            if (existing.LockedByUserId == userId)
            {
                existing.LockedUntil = now.Add(LockTtl);
                await _db.SaveChangesAsync(ct);
                return new AcquireLockResponse(true, MapToDto(existing));
            }

            // Блокировка принадлежит другому, но истекла — перехватываем
            if (existing.LockedUntil <= now)
            {
                existing.LockedByUserId = userId;
                existing.LockedByDisplayName = userDisplayName;
                existing.LockedAt = now;
                existing.LockedUntil = now.Add(LockTtl);
                await _db.SaveChangesAsync(ct);
                return new AcquireLockResponse(true, MapToDto(existing));
            }

            // Активная блокировка другого пользователя
            return new AcquireLockResponse(false, MapToDto(existing));
        }

        // Блокировки нет — создаём
        var newLock = new BpmDiagramLock
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            LockedByUserId = userId,
            LockedByDisplayName = userDisplayName,
            LockedAt = now,
            LockedUntil = now.Add(LockTtl),
        };
        _db.BpmDiagramLocks.Add(newLock);
        await _db.SaveChangesAsync(ct);
        return new AcquireLockResponse(true, MapToDto(newLock));
    }

    /// <inheritdoc />
    public async Task<bool> RefreshAsync(Guid processId, Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var @lock = await _db.BpmDiagramLocks
            .FirstOrDefaultAsync(l => l.ProcessId == processId && l.LockedByUserId == userId, ct);

        if (@lock is null || @lock.LockedUntil <= now)
            return false;

        @lock.LockedUntil = now.Add(LockTtl);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseAsync(Guid processId, Guid userId, CancellationToken ct = default)
    {
        var @lock = await _db.BpmDiagramLocks
            .FirstOrDefaultAsync(l => l.ProcessId == processId && l.LockedByUserId == userId, ct);

        if (@lock is null)
            return false;

        _db.BpmDiagramLocks.Remove(@lock);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Строка уже удалена параллельным запросом — блокировка снята, это нормально
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public async Task ForceReleaseAsync(Guid processId, CancellationToken ct = default)
    {
        var @lock = await _db.BpmDiagramLocks
            .FirstOrDefaultAsync(l => l.ProcessId == processId, ct);

        if (@lock is not null)
        {
            _db.BpmDiagramLocks.Remove(@lock);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Строка уже удалена параллельным запросом — блокировка снята, это нормально
            }
        }
    }

    private static DiagramLockDto MapToDto(BpmDiagramLock l) =>
        new(l.ProcessId, l.LockedByUserId, l.LockedByDisplayName, l.LockedAt, l.LockedUntil);
}
