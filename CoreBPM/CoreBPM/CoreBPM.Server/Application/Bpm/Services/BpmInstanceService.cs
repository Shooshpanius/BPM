using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления экземплярами бизнес-процессов.</summary>
public class BpmInstanceService : IBpmInstanceService
{
    private readonly AppDbContext _db;

    public BpmInstanceService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmInstanceListItemDto>> GetInstancesAsync(
        Guid processId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var skip = (Math.Max(1, page) - 1) * Math.Min(100, pageSize);

        var instances = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => i.ProcessId == processId)
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .OrderByDescending(i => i.StartedAt)
            .Skip(skip)
            .Take(Math.Min(100, pageSize))
            .ToListAsync(ct);

        var userIds = instances
            .SelectMany(i => new[] { i.InitiatorUserId, i.ResponsibleUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetDisplayName(Guid? userId)
        {
            if (userId == null) return null;
            return userNames.FirstOrDefault(u => u.Id == userId)?.DisplayName;
        }

        return instances.Select(i => new BpmInstanceListItemDto(
            i.Id,
            i.ProcessId,
            i.Process.Name,
            i.ProcessVersionId,
            i.ProcessVersion.VersionNumber,
            i.Name,
            i.State,
            i.LaunchSource,
            i.InitiatorUserId,
            GetDisplayName(i.InitiatorUserId),
            i.ResponsibleUserId,
            GetDisplayName(i.ResponsibleUserId),
            i.StartedAt,
            i.CompletedAt,
            i.CancelledAt
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .AsNoTracking()
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        return await MapToDtoAsync(instance, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> CreateInstanceAsync(
        Guid processId,
        CreateInstanceRequest request,
        Guid initiatorUserId,
        CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        if (!process.LaunchFromPortalEnabled)
            throw new ForbiddenException("Запуск процесса из портала отключён в настройках");

        // Ищем активную версию
        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == processId && v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("Процесс не имеет активной опубликованной версии");

        var name = ResolveInstanceName(process, request);

        var now = DateTimeOffset.UtcNow;
        var instance = new BpmInstance
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            ProcessVersionId = version.Id,
            Name = name,
            State = BpmInstanceState.Active,
            LaunchSource = BpmInstanceLaunchSource.Manual,
            InitiatorUserId = initiatorUserId,
            ResponsibleUserId = initiatorUserId,
            ExternalReference = request.ExternalReference,
            StartedAt = now,
            UpdatedAt = now,
        };

        // Начальные переменные
        var variables = BuildVariables(instance.Id, request.Variables, now);
        instance.Variables = variables;

        _db.BpmInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        return await GetInstanceByIdAsync(instance.Id, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> CreateInstanceViaWebhookAsync(
        string webhookKey,
        WebhookLaunchRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webhookKey))
            throw new ValidationException("Ключ вебхука не может быть пустым");

        var tokenHash = ComputeSha256Hash(webhookKey);

        var process = await _db.BpmProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ExternalStartTokenHash == tokenHash && p.ExternalStartEnabled, ct)
            ?? throw new NotFoundException("Процесс с указанным ключом вебхука не найден или внешний запуск отключён");

        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == process.Id && v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("Процесс не имеет активной опубликованной версии");

        var now = DateTimeOffset.UtcNow;
        var instance = new BpmInstance
        {
            Id = Guid.NewGuid(),
            ProcessId = process.Id,
            ProcessVersionId = version.Id,
            Name = string.IsNullOrWhiteSpace(process.InstanceNameTemplate)
                ? $"{process.Name} — {now:dd.MM.yyyy HH:mm}"
                : process.InstanceNameTemplate,
            State = BpmInstanceState.Active,
            LaunchSource = BpmInstanceLaunchSource.Webhook,
            InitiatorUserId = null,
            ResponsibleUserId = null,
            ExternalReference = request.ExternalReference,
            StartedAt = now,
            UpdatedAt = now,
        };

        var variables = BuildVariables(instance.Id, request.Variables, now);
        instance.Variables = variables;

        _db.BpmInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        return await GetInstanceByIdAsync(instance.Id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmSchedulerJobDto>> GetSchedulerJobsAsync(Guid processId, CancellationToken ct = default)
    {
        var jobs = await _db.BpmSchedulerJobs
            .AsNoTracking()
            .Where(j => j.ProcessId == processId)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);

        return jobs.Select(j => new BpmSchedulerJobDto(
            j.Id,
            j.ProcessId,
            j.ProcessVersionId,
            j.ElementId,
            j.TimerType,
            j.TimerValue,
            j.TimeZone,
            j.IsActive,
            j.LastFiredAt,
            j.NextFireAt,
            j.CreatedAt,
            j.UpdatedAt
        )).ToList();
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static string ResolveInstanceName(BpmProcess process, CreateInstanceRequest request)
    {
        return process.InstanceNameMode switch
        {
            BpmInstanceNameMode.Template when !string.IsNullOrWhiteSpace(process.InstanceNameTemplate)
                => process.InstanceNameTemplate,
            BpmInstanceNameMode.Manual when !string.IsNullOrWhiteSpace(request.Name)
                => request.Name!.Trim(),
            _ => $"{process.Name} — {DateTimeOffset.UtcNow:dd.MM.yyyy HH:mm}",
        };
    }

    private static List<BpmInstanceVariable> BuildVariables(
        Guid instanceId,
        IDictionary<string, string?>? variables,
        DateTimeOffset now)
    {
        if (variables == null || variables.Count == 0)
            return new List<BpmInstanceVariable>();

        return variables.Select(kv => new BpmInstanceVariable
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            ProcessVariableId = null,
            Name = kv.Key.Trim(),
            ValueJson = kv.Value,
            SetAt = now,
        }).ToList();
    }

    private async Task<BpmInstanceDto> MapToDtoAsync(BpmInstance i, CancellationToken ct)
    {
        var userIds = new[] { i.InitiatorUserId, i.ResponsibleUserId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var userNames = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetDisplayName(Guid? userId)
        {
            if (userId == null) return null;
            return userNames.FirstOrDefault(u => u.Id == userId)?.DisplayName;
        }

        return new BpmInstanceDto(
            i.Id,
            i.ProcessId,
            i.Process.Name,
            i.ProcessVersionId,
            i.ProcessVersion.VersionNumber,
            i.Name,
            i.State,
            i.LaunchSource,
            i.InitiatorUserId,
            GetDisplayName(i.InitiatorUserId),
            i.ResponsibleUserId,
            GetDisplayName(i.ResponsibleUserId),
            i.ParentInstanceId,
            i.ExternalReference,
            i.CancelReason,
            i.StartedAt,
            i.CompletedAt,
            i.CancelledAt,
            i.UpdatedAt,
            i.Variables.Select(v => new BpmInstanceVariableDto(v.Id, v.Name, v.ValueJson)).ToList()
        );
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
