using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер автоматической очистки журнала доставки уведомлений (FR-MSG-02.2).
/// Запускается ежедневно, удаляет записи старше срока, указанного в настройках retention.
/// По умолчанию срок хранения — 90 дней.
/// </summary>
public class NotificationLogCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationLogCleanupWorker> _logger;

    public NotificationLogCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationLogCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Первый запуск — через 5 минут после старта приложения
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка при очистке журнала доставки уведомлений.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationSettingsService>();

        var retention = await service.GetRetentionSettingsAsync(ct);
        if (retention.RetentionDays <= 0)
        {
            _logger.LogDebug("Автоочистка журнала уведомлений отключена (RetentionDays=0).");
            return;
        }

        var deleted = await service.PurgeOldLogsAsync(retention.RetentionDays, ct);

        if (deleted > 0)
            _logger.LogInformation(
                "Автоочистка журнала уведомлений: удалено {Count} записей старше {Days} дней.",
                deleted, retention.RetentionDays);
    }
}
