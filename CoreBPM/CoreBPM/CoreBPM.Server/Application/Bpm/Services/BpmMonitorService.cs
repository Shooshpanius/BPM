using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса монитора бизнес-процессов.</summary>
public class BpmMonitorService : IBpmMonitorService
{
    private readonly AppDbContext _db;

    public BpmMonitorService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessMonitorItemDto>> GetMyMonitorProcessesAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        // Идентификаторы процессов, где пользователь — Владелец или Куратор (по AssigneeId = userId)
        var monitorProcessIds = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => r.AssigneeType == BpmAssigneeType.User && r.AssigneeId == userId.ToString())
            .Select(r => r.ProcessId)
            .Distinct()
            .ToListAsync(ct);

        return await BuildMonitorListAsync(
            q => q.Where(p => monitorProcessIds.Contains(p.Id)),
            ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessMonitorItemDto>> GetFullMonitorProcessesAsync(
        CancellationToken ct = default)
    {
        return await BuildMonitorListAsync(q => q, ct);
    }

    /// <inheritdoc />
    public async Task<BpmProcessStatsDto> GetProcessStatsAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses
            .AsNoTracking()
            .Include(p => p.Versions)
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        var stateCounts = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => i.ProcessId == processId)
            .GroupBy(i => i.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var roles = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => r.ProcessId == processId)
            .ToListAsync(ct);

        var activeVersion = process.Versions
            .Where(v => v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault();

        return new BpmProcessStatsDto(
            ActiveCount:    Count(BpmInstanceState.Active),
            SuspendedCount: Count(BpmInstanceState.Suspended),
            CompletedCount: Count(BpmInstanceState.Completed),
            CancelledCount: Count(BpmInstanceState.Cancelled),
            TotalCount:     stateCounts.Sum(s => s.Count),
            ProcessName:    process.Name,
            ProcessDescription: process.Description,
            ActiveVersionNumber: activeVersion?.VersionNumber,
            PublishedAt:    activeVersion?.PublishedAt,
            CreatedAt:      process.CreatedAt,
            Owners:         roles
                .Where(r => r.RoleType == BpmProcessRoleType.Owner)
                .Select(r => r.DisplayName)
                .ToList(),
            Curators:       roles
                .Where(r => r.RoleType == BpmProcessRoleType.Curator)
                .Select(r => r.DisplayName)
                .ToList()
        );

        int Count(BpmInstanceState s) => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0;
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private async Task<IReadOnlyList<BpmProcessMonitorItemDto>> BuildMonitorListAsync(
        Func<IQueryable<BpmProcess>, IQueryable<BpmProcess>> filter,
        CancellationToken ct)
    {
        var processes = await filter(_db.BpmProcesses
            .AsNoTracking()
            .Include(p => p.Versions)
            .Where(p => !p.IsTemplate)
            .OrderBy(p => p.Name))
            .ToListAsync(ct);

        if (processes.Count == 0)
            return [];

        var processIds = processes.Select(p => p.Id).ToList();

        // Статистика экземпляров одним запросом
        var stateCounts = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => processIds.Contains(i.ProcessId))
            .GroupBy(i => new { i.ProcessId, i.State })
            .Select(g => new { g.Key.ProcessId, g.Key.State, Count = g.Count() })
            .ToListAsync(ct);

        // Роли
        var roles = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => processIds.Contains(r.ProcessId))
            .ToListAsync(ct);

        return processes.Select(p =>
        {
            var counts = stateCounts.Where(s => s.ProcessId == p.Id).ToList();
            var processRoles = roles.Where(r => r.ProcessId == p.Id).ToList();
            var activeVersion = p.Versions
                .Where(v => v.Status == BpmProcessVersionStatus.Active)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefault();

            return new BpmProcessMonitorItemDto(
                ProcessId:          p.Id,
                ProcessName:        p.Name,
                ProcessDescription: p.Description,
                ActiveVersionNumber: activeVersion?.VersionNumber,
                PublishedAt:        activeVersion?.PublishedAt,
                ActiveCount:    Count(BpmInstanceState.Active),
                SuspendedCount: Count(BpmInstanceState.Suspended),
                CompletedCount: Count(BpmInstanceState.Completed),
                CancelledCount: Count(BpmInstanceState.Cancelled),
                Owners:   processRoles.Where(r => r.RoleType == BpmProcessRoleType.Owner).Select(r => r.DisplayName).ToList(),
                Curators: processRoles.Where(r => r.RoleType == BpmProcessRoleType.Curator).Select(r => r.DisplayName).ToList()
            );

            int Count(BpmInstanceState s) => counts.FirstOrDefault(c => c.State == s)?.Count ?? 0;
        }).ToList();
    }
}
