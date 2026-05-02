using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления очередью исполнения.</summary>
public class BpmQueueService : IBpmQueueService
{
    private readonly AppDbContext _db;

    public BpmQueueService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmExecutionJobDto>> GetQueueAsync(
        BpmJobStatus? status,
        string? instanceName,
        Guid? processId,
        bool includeScheduled,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.BpmExecutionJobs
            .AsNoTracking()
            .Include(j => j.Process)
            .Include(j => j.Instance)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }
        else if (!includeScheduled)
        {
            // По умолчанию скрываем успешно завершённые
            query = query.Where(j => j.Status != BpmJobStatus.Completed);
        }

        if (processId.HasValue)
            query = query.Where(j => j.ProcessId == processId.Value);

        if (!string.IsNullOrWhiteSpace(instanceName))
            query = query.Where(j => j.Instance != null && j.Instance.Name.Contains(instanceName));

        var skip = (Math.Max(1, page) - 1) * Math.Min(100, pageSize);
        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(Math.Min(100, pageSize))
            .ToListAsync(ct);

        return jobs.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<QueueStatsDto> GetQueueStatsAsync(CancellationToken ct = default)
    {
        var counts = await _db.BpmExecutionJobs
            .AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Get(BpmJobStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;

        var pending   = Get(BpmJobStatus.Pending);
        var running   = Get(BpmJobStatus.Running);
        var scheduled = Get(BpmJobStatus.Scheduled);
        var failed    = Get(BpmJobStatus.Failed);
        return new QueueStatsDto(pending, running, scheduled, failed, pending + running + scheduled + failed);
    }

    /// <inheritdoc />
    public async Task RetryJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.BpmExecutionJobs.FindAsync([jobId], ct)
            ?? throw new NotFoundException($"Задание {jobId} не найдено");

        // Сбрасываем в Pending — движок подхватит при следующем обходе
        job.Status      = BpmJobStatus.Pending;
        job.NextRunAt   = DateTimeOffset.UtcNow;
        job.LastError   = null;
        job.UpdatedAt   = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task CancelTimerAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.BpmExecutionJobs.FindAsync([jobId], ct)
            ?? throw new NotFoundException($"Задание {jobId} не найдено");

        if (!job.IsTimer)
            throw new ValidationException("Операция доступна только для таймерных заданий");

        // Помечаем как Cancelled — обрабатывается движком
        job.Status    = BpmJobStatus.Failed;
        job.LastError = "Отменено администратором";
        job.FailedAt  = DateTimeOffset.UtcNow;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RescheduleTimerAsync(Guid jobId, DateTimeOffset newRunAt, CancellationToken ct = default)
    {
        var job = await _db.BpmExecutionJobs.FindAsync([jobId], ct)
            ?? throw new NotFoundException($"Задание {jobId} не найдено");

        if (!job.IsTimer)
            throw new ValidationException("Перенос времени доступен только для таймерных заданий");

        if (job.Status == BpmJobStatus.Failed)
            throw new ValidationException("Нельзя перенести задание в статусе «Ошибка»");

        job.NextRunAt = newRunAt;
        job.Status    = BpmJobStatus.Scheduled;
        job.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NodeAnalyticsDto>> GetNodeAnalyticsAsync(
        Guid processId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        // Агрегируем по записям истории с типом NodeExecuted/NodeFailed в экземплярах этого процесса
        var instanceIds = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => i.ProcessId == processId)
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (instanceIds.Count == 0) return [];

        var query = _db.BpmInstanceHistoryEntries
            .AsNoTracking()
            .Where(h => instanceIds.Contains(h.InstanceId)
                     && (h.EventType == BpmHistoryEventType.NodeExecuted
                      || h.EventType == BpmHistoryEventType.NodeFailed)
                     && h.ElementId != null);

        if (from.HasValue) query = query.Where(h => h.OccurredAt >= from.Value);
        if (to.HasValue)   query = query.Where(h => h.OccurredAt <= to.Value);

        var entries = await query.ToListAsync(ct);

        var result = entries
            .GroupBy(h => h.ElementId!)
            .Select(g =>
            {
                var executions = g.Where(h => h.EventType == BpmHistoryEventType.NodeExecuted).ToList();
                var errors     = g.Count(h => h.EventType == BpmHistoryEventType.NodeFailed);
                var durations  = executions
                    .Where(h => h.DurationMs.HasValue)
                    .Select(h => (double)h.DurationMs!.Value)
                    .OrderBy(d => d)
                    .ToList();

                return new NodeAnalyticsDto(
                    ElementId:      g.Key,
                    ElementName:    g.FirstOrDefault()?.ElementName,
                    ExecutionCount: executions.Count,
                    AvgDurationMs:  durations.Count > 0 ? durations.Average() : 0,
                    P50DurationMs:  Percentile(durations, 0.5),
                    P95DurationMs:  Percentile(durations, 0.95),
                    ErrorCount:     errors
                );
            })
            .OrderByDescending(n => n.ExecutionCount)
            .ToList();

        return result;
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(idx, sorted.Count - 1))];
    }

    private static BpmExecutionJobDto MapToDto(BpmExecutionJob j) => new(
        j.Id,
        j.ProcessId,
        j.Process.Name,
        j.InstanceId,
        j.Instance?.Name,
        j.ElementId,
        j.ElementType,
        j.OperationName,
        j.Status,
        j.AttemptNumber,
        j.MaxAttempts,
        j.NextRunAt,
        j.StartedAt,
        j.CompletedAt,
        j.FailedAt,
        j.LastError,
        j.ServerHost,
        j.IsTimer,
        j.TimerDeadline,
        j.CreatedAt
    );

    // ─── Retry-политики ───────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobRetryPolicyDto>> ExportRetryPoliciesAsync(
        CancellationToken ct = default)
    {
        var configs = await _db.BpmElementConfigs
            .AsNoTracking()
            .Include(c => c.Process)
            .OrderBy(c => c.Process.Name)
            .ThenBy(c => c.ElementId)
            .ToListAsync(ct);

        var result = new List<JobRetryPolicyDto>();
        foreach (var config in configs)
        {
            var fields = ParseRetryFields(config.ConfigJson);
            // Включаем запись только если задана хотя бы одна retry-настройка
            if (fields.MaxRetries <= 0 && fields.RetryDelaySeconds <= 0 && fields.BoundaryErrorCode == null)
                continue;

            result.Add(new JobRetryPolicyDto(
                ProcessId:          config.ProcessId,
                ProcessName:        config.Process.Name,
                ElementId:          config.ElementId,
                ElementName:        null,
                MaxRetries:         fields.MaxRetries,
                RetryDelaySeconds:  fields.RetryDelaySeconds,
                BoundaryErrorCode:  fields.BoundaryErrorCode
            ));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task ImportRetryPoliciesAsync(
        ImportRetryPoliciesRequest request,
        CancellationToken ct = default)
    {
        if (request.Policies.Count == 0) return;

        var now = DateTimeOffset.UtcNow;

        // Загружаем существующие конфигурации одним запросом
        var processIds  = request.Policies.Select(p => p.ProcessId).Distinct().ToList();
        var elementIds  = request.Policies.Select(p => p.ElementId).Distinct().ToList();

        var existing = await _db.BpmElementConfigs
            .Where(c => processIds.Contains(c.ProcessId) && elementIds.Contains(c.ElementId))
            .ToListAsync(ct);

        foreach (var policy in request.Policies)
        {
            var config = existing.FirstOrDefault(
                c => c.ProcessId == policy.ProcessId && c.ElementId == policy.ElementId);

            if (config == null)
            {
                config = new BpmElementConfig
                {
                    Id         = Guid.NewGuid(),
                    ProcessId  = policy.ProcessId,
                    ElementId  = policy.ElementId,
                    ConfigJson = "{}",
                    UpdatedAt  = now,
                };
                _db.BpmElementConfigs.Add(config);
            }

            config.ConfigJson = MergeRetryFields(config.ConfigJson, policy);
            config.UpdatedAt  = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Извлекает retry-поля из JSON-конфигурации элемента.</summary>
    private static (int MaxRetries, int RetryDelaySeconds, string? BoundaryErrorCode) ParseRetryFields(
        string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            var maxRetries = root.TryGetProperty("maxRetries", out var mr) && mr.TryGetInt32(out var mrv)
                ? mrv : 0;
            var retryDelay = root.TryGetProperty("retryDelaySeconds", out var rd) && rd.TryGetInt32(out var rdv)
                ? rdv : 0;
            var errorCode = root.TryGetProperty("boundaryErrorCode", out var be) && be.ValueKind == JsonValueKind.String
                ? be.GetString() : null;

            return (maxRetries, retryDelay, errorCode);
        }
        catch
        {
            return (0, 0, null);
        }
    }

    /// <summary>Объединяет retry-поля политики с существующим JSON-объектом конфигурации элемента.</summary>
    private static string MergeRetryFields(string existingJson, JobRetryPolicyDto policy)
    {
        JsonObject node;
        try
        {
            node = JsonNode.Parse(existingJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            node = new JsonObject();
        }

        node["maxRetries"]        = policy.MaxRetries;
        node["retryDelaySeconds"] = policy.RetryDelaySeconds;

        if (!string.IsNullOrEmpty(policy.BoundaryErrorCode))
            node["boundaryErrorCode"] = policy.BoundaryErrorCode;
        else
            node.Remove("boundaryErrorCode");

        return node.ToJsonString();
    }
}
