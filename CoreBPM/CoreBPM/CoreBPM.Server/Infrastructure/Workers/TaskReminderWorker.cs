using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер персональных напоминаний по задачам (FR-TASK-01.1).
/// Запускается каждую минуту. Отправляет SignalR-уведомление «TaskReminder»
/// каждому пользователю, у которого наступило время напоминания (<c>RemindAt &lt;= now</c>),
/// и выставляет <c>IsSent = true</c>.
/// </summary>
public class TaskReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskReminderWorker> _logger;

    public TaskReminderWorker(IServiceScopeFactory scopeFactory, ILogger<TaskReminderWorker> logger)
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
                await SendRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка при отправке напоминаний по задачам.");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<IBpmNotificationService>();
        var now = DateTimeOffset.UtcNow;

        var reminders = await db.TaskReminders
            .Include(r => r.Task)
            .Where(r => !r.IsSent && r.RemindAt <= now)
            .ToListAsync(ct);

        if (reminders.Count == 0) return;

        foreach (var reminder in reminders)
        {
            await notifications.NotifyUserAsync(
                reminder.UserId,
                "TaskReminder",
                new
                {
                    taskId = reminder.TaskId,
                    subject = reminder.Task.Subject,
                    dueDate = reminder.Task.DueDate,
                },
                ct);

            reminder.IsSent = true;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Отправлено напоминаний по задачам: {Count}.", reminders.Count);
    }
}
