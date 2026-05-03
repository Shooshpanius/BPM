using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Infrastructure.Persistence;
using TaskStatus = CoreBPM.Server.Domain.Tasks.TaskStatus;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер автоматической установки флага просрочки задач (FR-TASK-01.2).
/// Запускается каждый час. Находит задачи с истёкшим DueDate в нефинальном статусе
/// и выставляет IsOverdue = true.
/// </summary>
public class TaskOverdueWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private static readonly HashSet<TaskStatus> FinalStatuses = new()
    {
        TaskStatus.Done, TaskStatus.DoneControlled,
        TaskStatus.CannotDo, TaskStatus.CannotDoControlled,
        TaskStatus.Closed,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskOverdueWorker> _logger;

    public TaskOverdueWorker(IServiceScopeFactory scopeFactory, ILogger<TaskOverdueWorker> logger)
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
                await CheckOverdueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка при проверке просрочки задач.");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task CheckOverdueAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        var tasks = await db.TaskItems
            .Where(t => !t.IsOverdue && t.DueDate < now && !FinalStatuses.Contains(t.Status))
            .ToListAsync(ct);

        if (tasks.Count == 0) return;

        foreach (var task in tasks)
        {
            task.IsOverdue = true;
            task.UpdatedAt = now;
            db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                // Guid.Empty означает системное действие (не пользователь)
                ActorUserId = Guid.Empty,
                Action = TaskHistoryAction.StatusChanged,
                FieldName = "IsOverdue",
                OldValue = "false",
                NewValue = "true",
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Флаг просрочки установлен для {Count} задач.", tasks.Count);
    }
}
