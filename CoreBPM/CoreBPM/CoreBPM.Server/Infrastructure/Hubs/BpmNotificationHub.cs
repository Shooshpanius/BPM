using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CoreBPM.Server.Infrastructure.Hubs;

/// <summary>
/// SignalR-хаб для доставки in-app уведомлений BPM в реальном времени.
/// Клиенты подключаются по адресу /hubs/bpm-notifications.
/// </summary>
[Authorize]
public class BpmNotificationHub : Hub
{
    /// <summary>Имя группы, в которую добавляются все администраторы.</summary>
    public const string AdminGroup = "admin";

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        // Администраторы добавляются в отдельную группу для уведомлений очереди
        if (Context.User?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

        await base.OnConnectedAsync();
    }
}
