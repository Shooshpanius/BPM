using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Workers;

/// <summary>
/// Фоновый воркер мониторинга KPI процессов (FR-BPM-03.2).
/// Каждый час проверяет среднее время цикла по последним N завершённым экземплярам.
/// Если avg > TargetCycleTimeMinutes * 1.2, создаёт алерт и уведомляет владельцев процесса.
/// </summary>
public class BpmKpiAlertWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromHours(1);

    /// <summary>Количество последних завершённых экземпляров для вычисления avg cycle time.</summary>
    private const int SampleSize = 20;

    /// <summary>Порог превышения относительно цели (20%).</summary>
    private const double ThresholdMultiplier = 1.2;

    /// <summary>Минимальное количество завершённых экземпляров для расчёта алерта.</summary>
    private const int MinCompletedInstances = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BpmKpiAlertWorker> _logger;

    public BpmKpiAlertWorker(IServiceScopeFactory scopeFactory, ILogger<BpmKpiAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BpmKpiAlertWorker запущен");

        // Первый запуск — небольшая задержка, чтобы не мешать старту приложения
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckKpiThresholdsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Ошибка в BpmKpiAlertWorker");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task CheckKpiThresholdsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<IBpmNotificationService>();

        // Загружаем процессы с заданным TargetCycleTimeMinutes
        var processes = await db.BpmProcesses.AsNoTracking()
            .Where(p => !p.IsDeleted && p.TargetCycleTimeMinutes != null)
            .Select(p => new { p.Id, p.Name, p.TargetCycleTimeMinutes })
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var alertsToSave = new List<BpmKpiAlert>();

        foreach (var process in processes)
        {
            // Берём последние SampleSize завершённых экземпляров
            var cycleTimes = await db.BpmInstances.AsNoTracking()
                .Where(i => i.ProcessId == process.Id
                            && i.State == BpmInstanceState.Completed
                            && i.CompletedAt != null)
                .OrderByDescending(i => i.CompletedAt)
                .Take(SampleSize)
                .Select(i => new { i.StartedAt, i.CompletedAt })
                .ToListAsync(ct);

            if (cycleTimes.Count < MinCompletedInstances) continue;

            var avgMinutes = cycleTimes
                .Select(i => (i.CompletedAt!.Value - i.StartedAt).TotalMinutes)
                .Average();

            var target = process.TargetCycleTimeMinutes!.Value;
            if (avgMinutes <= target * ThresholdMultiplier) continue;

            // Проверяем, что за последние 2 часа по этому процессу ещё не было алерта
            var recentAlert = await db.BpmKpiAlerts.AsNoTracking()
                .AnyAsync(a => a.ProcessId == process.Id && a.DetectedAt >= now.AddHours(-2), ct);
            if (recentAlert) continue;

            var exceedPercent = Math.Round((avgMinutes / target - 1) * 100, 1);

            var alert = new BpmKpiAlert
            {
                Id = Guid.NewGuid(),
                ProcessId = process.Id,
                ProcessName = process.Name,
                AvgCycleTimeMinutes = Math.Round(avgMinutes, 2),
                TargetCycleTimeMinutes = target,
                ExceedPercent = exceedPercent,
                DetectedAt = now,
            };
            alertsToSave.Add(alert);

            _logger.LogWarning(
                "KPI-алерт: процесс «{Process}» — avg {Avg:F1} мин > цель {Target:F1} мин * {Threshold} (превышение +{Exceed}%)",
                process.Name, avgMinutes, target, ThresholdMultiplier, exceedPercent);

            // Уведомляем владельцев процесса
            try
            {
                var ownerIds = await db.BpmProcessRoleConfigs.AsNoTracking()
                    .Where(r => r.ProcessId == process.Id
                                && r.RoleType == BpmProcessRoleType.Owner
                                && r.AssigneeType == BpmAssigneeType.User
                                && r.AssigneeId != null && r.AssigneeId != string.Empty)
                    .Select(r => r.AssigneeId)
                    .ToListAsync(ct);

                foreach (var assigneeIdStr in ownerIds)
                {
                    if (!Guid.TryParse(assigneeIdStr, out var ownerId)) continue;
                    await notifications.NotifyUserAsync(
                        ownerId,
                        "KpiThresholdExceeded",
                        new
                        {
                            processId = process.Id,
                            processName = process.Name,
                            avgCycleTimeMinutes = Math.Round(avgMinutes, 2),
                            targetCycleTimeMinutes = target,
                            exceedPercent,
                        },
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке KPI-уведомления для процесса {ProcessId}", process.Id);
            }
        }

        if (alertsToSave.Count > 0)
        {
            db.BpmKpiAlerts.AddRange(alertsToSave);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Создано {Count} KPI-алертов", alertsToSave.Count);
        }
    }
}
