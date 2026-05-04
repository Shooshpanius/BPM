using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Hubs;

/// <summary>
/// SignalR-хаб для доставки in-app уведомлений BPM в реальном времени.
/// Клиенты подключаются по адресу /hubs/bpm-notifications.
/// </summary>
[Authorize]
public class BpmNotificationHub : Hub
{
    private readonly AppDbContext _db;
    private readonly ILogger<BpmNotificationHub> _logger;

    /// <summary>Имя группы, в которую добавляются все администраторы.</summary>
    public const string AdminGroup = "admin";

    public BpmNotificationHub(AppDbContext db, ILogger<BpmNotificationHub> logger)
    {
        _db = db;
        _logger = logger;
    }

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
    /// Подписка разрешена только участникам чата (защита от IDOR).
    /// </summary>
    public async Task JoinChat(string chatId)
    {
        if (!Guid.TryParse(chatId, out var id)) return;

        var userIdStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return;

        var isMember = await _db.NotifyChatMembers
            .AnyAsync(m => m.ChatId == id && m.UserId == userId);

        if (!isMember)
        {
            _logger.LogWarning("Пользователь {UserId} попытался подписаться на чат {ChatId} без членства.", userId, chatId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{chatId}");
    }

    /// <summary>Клиент отписывается от real-time событий чата.</summary>
    public async Task LeaveChat(string chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:{chatId}");
    }
}
