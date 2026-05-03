using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Infrastructure.Persistence;
using TaskStatus = CoreBPM.Server.Domain.Tasks.TaskStatus;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер автоматического возврата отложенных задач в статус «Новая» (FR-TASK-01.2).
/// Запускается каждую минуту. При наступлении PostponedUntil переводит задачу
/// из Postponed → New и очищает PostponedUntil.
/// </summary>
public class TaskPostponeWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskPostponeWorker> _logger;

    public TaskPostponeWorker(IServiceScopeFactory scopeFactory, ILogger<TaskPostponeWorker> logger)
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
                await ResumePostponedAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка при возврате отложенных задач.");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ResumePostponedAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var tasks = await db.TaskItems
            .Where(t => t.Status == TaskStatus.Postponed
                        && t.PostponedUntil.HasValue
                        && t.PostponedUntil.Value <= now)
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        foreach (var task in tasks)
        {
            task.Status = TaskStatus.New;
            task.PostponedUntil = null;
            task.UpdatedAt = now;
            db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                // Guid.Empty означает системное действие (не пользователь)
                ActorUserId = Guid.Empty,
                Action = TaskHistoryAction.StatusChanged,
                FieldName = "Status",
                OldValue = TaskStatus.Postponed.ToString(),
                NewValue = TaskStatus.New.ToString(),
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Возвращено из статуса «Отложена» в «Новая»: {Count} задач.", tasks.Count);
    }
}
