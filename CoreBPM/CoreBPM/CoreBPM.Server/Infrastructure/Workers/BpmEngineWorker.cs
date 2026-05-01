using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер движка BPMN.
/// Каждые 5 секунд захватывает Pending-задания с NextRunAt &lt;= now и выполняет их.
/// </summary>
public class BpmEngineWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BpmEngineWorker> _logger;
    private readonly string _serverHost;

    public BpmEngineWorker(IServiceScopeFactory scopeFactory, ILogger<BpmEngineWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _serverHost = $"{Environment.MachineName}:{Environment.ProcessId}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BpmEngineWorker запущен на хосте {ServerHost}", _serverHost);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка в цикле воркера BpmEngineWorker");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("BpmEngineWorker остановлен");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Атомарно захватываем пачку заданий (до 20 за раз)
        var jobs = await db.BpmExecutionJobs
            .Where(j =>
                (j.Status == BpmJobStatus.Pending || j.Status == BpmJobStatus.Scheduled) &&
                (j.NextRunAt == null || j.NextRunAt <= now))
            .OrderBy(j => j.NextRunAt)
            .Take(20)
            .ToListAsync(ct);

        if (jobs.Count == 0) return;

        // Устанавливаем статус Running и захватываем задания
        foreach (var job in jobs)
        {
            job.Status = BpmJobStatus.Running;
            job.AttemptNumber++;
            job.StartedAt = now;
            job.ServerHost = _serverHost;
            job.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        _logger.LogDebug("BpmEngineWorker: захвачено {Count} заданий", jobs.Count);

        // Выполняем каждое задание в отдельном scope
        var tasks = jobs.Select(job => ExecuteJobSafeAsync(job.Id, ct));
        await Task.WhenAll(tasks);
    }

    private async Task ExecuteJobSafeAsync(Guid jobId, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var engine = scope.ServiceProvider.GetRequiredService<IBpmExecutionEngine>();
            await engine.ExecuteJobAsync(jobId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BpmEngineWorker: необработанная ошибка при выполнении задания {JobId}", jobId);

            // Пытаемся пометить задание как Failed, чтобы не застряло в Running
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var job = await db.BpmExecutionJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
                if (job != null && job.Status == BpmJobStatus.Running)
                {
                    job.Status = BpmJobStatus.Failed;
                    job.LastError = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                    job.FailedAt = DateTimeOffset.UtcNow;
                    job.UpdatedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "BpmEngineWorker: не удалось обновить статус задания {JobId} после ошибки", jobId);
            }
        }
    }
}
