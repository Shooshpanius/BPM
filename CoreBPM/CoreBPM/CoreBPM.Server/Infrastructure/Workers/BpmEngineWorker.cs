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

    /// <summary>Максимальная длина сообщения об ошибке, сохраняемого в БД.</summary>
    private const int MaxErrorLength = 4000;

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
                await ProcessScheduledMigrationPackagesAsync(stoppingToken);
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

        // Захватываем пачку заданий (до 20 за раз)
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

        // Сохраняем с обработкой конкурентных конфликтов (строка могла быть удалена или изменена)
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                "BpmEngineWorker: конфликт при захвате заданий ({Count} конфликтов) — пропускаем конфликтующие",
                ex.Entries.Count);

            var conflictedIds = ex.Entries
                .Select(e => ((BpmExecutionJob)e.Entity).Id)
                .ToHashSet();

            // Отсоединяем конфликтные сущности и убираем их из списка
            foreach (var entry in ex.Entries)
                entry.State = EntityState.Detached;

            jobs.RemoveAll(j => conflictedIds.Contains(j.Id));

            if (jobs.Count == 0) return;

            await db.SaveChangesAsync(ct);
        }

        _logger.LogDebug("BpmEngineWorker: захвачено {Count} заданий", jobs.Count);

        // Группируем по InstanceId: задания одного экземпляра выполняем последовательно,
        // задания разных экземпляров — параллельно.
        // Это предотвращает гонку на AND-Join счётчиках и состоянии токенов
        // внутри одного экземпляра процесса.
        var groups = jobs
            .GroupBy(j => j.InstanceId ?? j.Id)
            .ToList();

        var tasks = groups.Select(group =>
        {
            var ids = group.Select(j => j.Id).ToList();
            return ExecuteGroupSafeAsync(ids, ct);
        });
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Выполняет группу заданий (одного экземпляра) последовательно,
    /// чтобы исключить конкурентные обновления токенов и шлюзовых счётчиков.
    /// Каждое задание выполняется независимо: ошибка в одном не останавливает остальные.
    /// </summary>
    private async Task ExecuteGroupSafeAsync(IReadOnlyList<Guid> jobIds, CancellationToken ct)
    {
        foreach (var jobId in jobIds)
        {
            if (ct.IsCancellationRequested) break;
            await ExecuteJobSafeAsync(jobId, ct);
        }
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
                    job.LastError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
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

    /// <summary>
    /// Проверяет пакеты миграции с расписанием и запускает те, у которых наступило время (ScheduledAt &lt;= now).
    /// </summary>
    private async Task ProcessScheduledMigrationPackagesAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Находим пакеты в статусе New с наступившим временем запуска
        var packages = await db.BpmVersionMigrationPackages
            .Where(p =>
                p.Status == BpmMigrationPackageStatus.New &&
                p.ScheduledAt != null &&
                p.ScheduledAt <= now)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (packages.Count == 0) return;

        _logger.LogInformation(
            "BpmEngineWorker: найдено {Count} пакетов миграции для автозапуска по расписанию",
            packages.Count);

        foreach (var packageId in packages)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await using var pkgScope = _scopeFactory.CreateAsyncScope();
                var migrationService = pkgScope.ServiceProvider
                    .GetRequiredService<IBpmMigrationService>();
                await migrationService.StartPackageAsync(packageId, ct);
                _logger.LogInformation(
                    "BpmEngineWorker: пакет миграции {PackageId} запущен по расписанию",
                    packageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "BpmEngineWorker: ошибка при автозапуске пакета миграции {PackageId}",
                    packageId);
            }
        }
    }
}
