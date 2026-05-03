using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Tasks.Interfaces;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый сервис, который ежедневно создаёт очередные экземпляры
/// периодических задач в соответствии с настройками серии (FR-TASK-01.5.1).
/// </summary>
public class TaskPeriodicWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskPeriodicWorker> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public TaskPeriodicWorker(IServiceScopeFactory scopeFactory, ILogger<TaskPeriodicWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRecurrencesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка при обработке периодических задач.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessRecurrencesAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskService>();

        var activeRecurrences = await db.TaskRecurrences.AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        foreach (var rec in activeRecurrences)
        {
            // Проверяем дату окончания серии
            if (rec.EndCondition == TaskSeriesEndCondition.ByDate && rec.EndDate.HasValue
                && DateTimeOffset.UtcNow >= rec.EndDate.Value)
            {
                var toStop = await db.TaskRecurrences.FirstOrDefaultAsync(r => r.Id == rec.Id, ct);
                if (toStop != null) { toStop.IsActive = false; await db.SaveChangesAsync(ct); }
                continue;
            }

            // Считаем активные экземпляры в серии
            var activeCount = await db.TaskItems.AsNoTracking()
                .CountAsync(t =>
                    (t.SeriesId == rec.Id || t.Id == rec.RootTaskId)
                    && t.Status != Domain.Tasks.TaskStatus.Done
                    && t.Status != Domain.Tasks.TaskStatus.Closed
                    && t.Status != Domain.Tasks.TaskStatus.CannotDo, ct);

            // Создаём пока не набрали нужное количество вперёд
            int needed = rec.LookAheadCount + 1 - activeCount;
            for (int i = 0; i < needed; i++)
            {
                var created = await taskService.CreateNextPeriodicInstanceAsync(rec.Id, ct);
                if (created == null) break;
                _logger.LogInformation("Создан экземпляр периодической задачи {TaskId} (серия {SeriesId})", created.Id, rec.Id);
            }
        }
    }
}
