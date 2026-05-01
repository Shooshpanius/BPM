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

    /// <inheritdoc />
    public async Task<byte[]> ExportProcessInstancesToCsvAsync(
        Guid processId,
        CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        var instances = await _db.BpmInstances
            .AsNoTracking()
            .Include(i => i.ProcessVersion)
            .Where(i => i.ProcessId == processId)
            .OrderByDescending(i => i.StartedAt)
            .Take(5000)
            .ToListAsync(ct);

        var userIds = instances
            .SelectMany(i => new[] { i.InitiatorUserId, i.ResponsibleUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetName(Guid? id) => userNames.FirstOrDefault(u => u.Id == id)?.DisplayName;

        return BuildCsv(
            ["Экземпляр", "Процесс", "Версия", "Состояние", "Инициатор", "Ответственный", "Запущен", "Завершён"],
            instances.Select(i => new[]
            {
                i.Name,
                process.Name,
                i.ProcessVersion.VersionNumber.ToString(),
                i.State.ToString(),
                GetName(i.InitiatorUserId) ?? "",
                GetName(i.ResponsibleUserId) ?? "",
                i.StartedAt.ToString("dd.MM.yyyy HH:mm"),
                (i.CompletedAt ?? i.CancelledAt)?.ToString("dd.MM.yyyy HH:mm") ?? "",
            })
        );
    }

    /// <inheritdoc />
    public async Task<BpmDashboardDto> GetDashboardAsync(
        Guid? userId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        // Определяем набор процессов, доступных пользователю
        IQueryable<Guid> processIdQuery;
        if (isAdmin)
        {
            processIdQuery = _db.BpmProcesses
                .Where(p => !p.IsTemplate)
                .Select(p => p.Id);
        }
        else if (userId.HasValue)
        {
            var monitorIds = await _db.BpmProcessRoleConfigs
                .AsNoTracking()
                .Where(r => r.AssigneeType == BpmAssigneeType.User && r.AssigneeId == userId.Value.ToString())
                .Select(r => r.ProcessId)
                .ToListAsync(ct);
            processIdQuery = _db.BpmProcesses
                .Where(p => !p.IsTemplate && monitorIds.Contains(p.Id))
                .Select(p => p.Id);
        }
        else
        {
            return new BpmDashboardDto(0, 0, 0, 0, 0, 0, 0, []);
        }

        var processIds = await processIdQuery.ToListAsync(ct);
        var totalProcesses = processIds.Count;

        // Агрегированная статистика экземпляров
        var stateCounts = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => processIds.Contains(i.ProcessId))
            .GroupBy(i => i.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(BpmInstanceState s) => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0;

        // Количество Failed-заданий в очереди
        var failedJobs = await _db.BpmExecutionJobs
            .AsNoTracking()
            .CountAsync(j => j.Status == BpmJobStatus.Failed && processIds.Contains(j.ProcessId), ct);

        // Топ-5 процессов по числу активных экземпляров
        var topProcesses = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => processIds.Contains(i.ProcessId) && i.State == BpmInstanceState.Active)
            .GroupBy(i => new { i.ProcessId, ProcessName = i.Process.Name })
            .Select(g => new { g.Key.ProcessId, g.Key.ProcessName, ActiveCount = g.Count() })
            .OrderByDescending(x => x.ActiveCount)
            .Take(5)
            .ToListAsync(ct);

        var totalPerProcess = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => topProcesses.Select(t => t.ProcessId).Contains(i.ProcessId))
            .GroupBy(i => i.ProcessId)
            .Select(g => new { ProcessId = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        return new BpmDashboardDto(
            TotalProcesses: totalProcesses,
            ActiveInstances: Count(BpmInstanceState.Active),
            SuspendedInstances: Count(BpmInstanceState.Suspended),
            CompletedInstances: Count(BpmInstanceState.Completed),
            CancelledInstances: Count(BpmInstanceState.Cancelled),
            FaultedInstances: Count(BpmInstanceState.Faulted),
            FailedJobs: failedJobs,
            TopActiveProcesses: topProcesses.Select(t => new BpmDashboardTopProcessDto(
                t.ProcessId, t.ProcessName, t.ActiveCount,
                totalPerProcess.FirstOrDefault(x => x.ProcessId == t.ProcessId)?.Total ?? t.ActiveCount
            )).ToList()
        );
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static byte[] BuildCsv(IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(";", headers.Select(EscapeCsv)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(";", row.Select(EscapeCsv)));
        var content = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return [0xEF, 0xBB, 0xBF, .. content];
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
