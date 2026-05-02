using System.Xml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер планировщика таймерных стартовых событий BPMN.
/// Каждые 30 секунд проверяет bpm_scheduler_jobs и создаёт новые экземпляры
/// процессов для заданий с истёкшим NextFireAt.
/// </summary>
public class BpmSchedulerWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);
    private const int MaxErrorLength = 4000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BpmSchedulerWorker> _logger;

    public BpmSchedulerWorker(IServiceScopeFactory scopeFactory, ILogger<BpmSchedulerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BpmSchedulerWorker запущен");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSchedulerJobsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка в цикле воркера BpmSchedulerWorker");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("BpmSchedulerWorker остановлен");
    }

    private async Task ProcessSchedulerJobsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var engine = scope.ServiceProvider.GetRequiredService<IBpmExecutionEngine>();

        var now = DateTimeOffset.UtcNow;

        // Получаем активные задания с истёкшим NextFireAt
        var jobs = await db.BpmSchedulerJobs
            .Where(j => j.IsActive && j.NextFireAt != null && j.NextFireAt <= now)
            .Include(j => j.Process)
            .ToListAsync(ct);

        if (jobs.Count == 0) return;

        _logger.LogDebug("BpmSchedulerWorker: найдено {Count} заданий для выполнения", jobs.Count);

        foreach (var job in jobs)
        {
            await FireJobAsync(db, engine, job, now, ct);
        }
    }

    private async Task FireJobAsync(
        AppDbContext db,
        IBpmExecutionEngine engine,
        BpmSchedulerJob job,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            // Создаём новый экземпляр процесса по таймерному стартовому событию
            var processName = job.Process?.Name ?? job.ProcessId.ToString();
            var instance = new BpmInstance
            {
                Id = Guid.NewGuid(),
                ProcessId = job.ProcessId,
                ProcessVersionId = job.ProcessVersionId,
                Name = $"{processName} — {now:dd.MM.yyyy HH:mm}",
                State = BpmInstanceState.Active,
                LaunchSource = BpmInstanceLaunchSource.Scheduler,
                StartedAt = now,
                UpdatedAt = now,
            };
            db.BpmInstances.Add(instance);

            // Обновляем состояние задания планировщика
            job.LastFiredAt = now;
            job.UpdatedAt = now;

            switch (job.TimerType)
            {
                case "timeCycle":
                    // Циклический таймер — вычисляем следующее срабатывание
                    var next = ComputeNextFireAt(job.TimerValue, now);
                    if (next.HasValue)
                    {
                        job.NextFireAt = next.Value;
                    }
                    else
                    {
                        // Не удалось вычислить следующее время — деактивируем
                        _logger.LogWarning(
                            "BpmSchedulerWorker: не удалось вычислить следующее время для задания {JobId} (значение: «{TimerValue}») — задание деактивировано",
                            job.Id, job.TimerValue);
                        job.IsActive = false;
                        job.NextFireAt = null;
                    }
                    break;

                case "timeDate":
                case "timeDuration":
                default:
                    // Одноразовые таймеры — деактивируем после первого срабатывания
                    job.IsActive = false;
                    job.NextFireAt = null;
                    break;
            }

            // Сбрасываем счётчик ошибок при успешном срабатывании
            job.RetryCount = 0;
            job.LastError = null;

            await db.SaveChangesAsync(ct);

            // Запускаем выполнение экземпляра асинхронно (fire-and-forget)
            _ = engine.StartAsync(instance.Id, CancellationToken.None);

            _logger.LogInformation(
                "BpmSchedulerWorker: запущен экземпляр {InstanceId} для процесса {ProcessId} " +
                "по заданию планировщика {JobId} (тип: {TimerType})",
                instance.Id, job.ProcessId, job.Id, job.TimerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "BpmSchedulerWorker: ошибка при выполнении задания {JobId} процесса {ProcessId}",
                job.Id, job.ProcessId);

            try
            {
                var errorNow = DateTimeOffset.UtcNow;
                job.RetryCount++;
                job.LastError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
                job.UpdatedAt = errorNow;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx,
                    "BpmSchedulerWorker: не удалось сохранить информацию об ошибке задания {JobId}", job.Id);
            }
        }
    }

    /// <summary>
    /// Вычисляет следующее время срабатывания для циклического таймера.
    /// Поддерживает ISO 8601 длительности (PT30M, P1DT2H) и repeating intervals (R/PT1H, R3/P1D).
    /// </summary>
    internal static DateTimeOffset? ComputeNextFireAt(string timerValue, DateTimeOffset from)
    {
        if (string.IsNullOrWhiteSpace(timerValue)) return null;

        var value = timerValue.Trim();

        // Формат R[n]/duration: повторяющийся ISO 8601 интервал
        if (value.StartsWith('R') || value.StartsWith('r'))
        {
            var slashIdx = value.IndexOf('/');
            if (slashIdx >= 0 && slashIdx < value.Length - 1)
                value = value[(slashIdx + 1)..];
        }

        // Попытка разобрать как ISO 8601 duration (PT30M, P1DT2H, P1D и т.д.)
        try
        {
            var span = XmlConvert.ToTimeSpan(value);
            if (span > TimeSpan.Zero)
                return from.Add(span);
        }
        catch
        {
            // Не является стандартной ISO 8601 duration — возвращаем null
        }

        return null;
    }
}
