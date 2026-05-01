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
