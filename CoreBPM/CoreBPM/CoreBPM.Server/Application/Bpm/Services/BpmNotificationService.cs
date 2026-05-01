using Microsoft.AspNetCore.SignalR;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Infrastructure.Hubs;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса уведомлений через SignalR.</summary>
public class BpmNotificationService : IBpmNotificationService
{
    private readonly IHubContext<BpmNotificationHub> _hub;

    public BpmNotificationService(IHubContext<BpmNotificationHub> hub)
    {
        _hub = hub;
    }

    /// <inheritdoc />
    public async Task NotifyJobFailedAsync(
        Guid jobId,
        string processName,
        string? instanceName,
        string? error,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "JobFailed",
            jobId,
            processName,
            instanceName,
            error,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc />
    public async Task NotifyMigrationPackageCompletedAsync(
        Guid packageId,
        string packageName,
        bool hasErrors,
        int total,
        int migrated,
        int failed,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "MigrationPackageCompleted",
            packageId,
            packageName,
            hasErrors,
            total,
            migrated,
            failed,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);

        // Уведомляем создателя пакета (если он не является администратором)
        await _hub.Clients
            .Group($"user:{createdByUserId}")
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc />
    public async Task NotifyUserTaskActivatedAsync(
        Guid instanceId,
        string instanceName,
        string processName,
        string elementId,
        string? elementName,
        CancellationToken ct = default)
    {
        var payload = new
        {
            type = "UserTaskActivated",
            instanceId,
            instanceName,
            processName,
            elementId,
            elementName,
            occurredAt = DateTimeOffset.UtcNow,
        };

        // Уведомляем всех участников процесса — они сами подпишутся на группы экземпляра
        await _hub.Clients
            .Group($"instance:{instanceId}")
            .SendAsync("bpm:notification", payload, ct);

        // Также уведомляем всех администраторов
        await _hub.Clients
            .Group(BpmNotificationHub.AdminGroup)
            .SendAsync("bpm:notification", payload, ct);
    }

    /// <inheritdoc />
    public async Task NotifyUserAsync(
        Guid userId,
        string eventType,
        object payload,
        CancellationToken ct = default)
    {
        await _hub.Clients
            .Group($"user:{userId}")
            .SendAsync("bpm:notification", new { type = eventType, data = payload }, ct);
    }
}
