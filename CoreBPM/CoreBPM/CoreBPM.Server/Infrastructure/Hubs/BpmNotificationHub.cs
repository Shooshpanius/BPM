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

    /// <summary>
    /// Клиент вызывает этот метод, чтобы подписаться на real-time события чата (FR-MSG-01.1).
    /// </summary>
    public async Task JoinChat(string chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{chatId}");
    }

    /// <summary>Клиент отписывается от real-time событий чата.</summary>
    public async Task LeaveChat(string chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:{chatId}");
    }
}
