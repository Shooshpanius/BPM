using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;
using CoreBPM.Server.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CoreBPM.Server.Controllers;

/// <summary>API чатов и сообщений (FR-MSG-01.1).</summary>
[ApiController]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessagingService _svc;
    private readonly IHubContext<BpmNotificationHub> _hub;

    public MessagesController(IMessagingService svc, IHubContext<BpmNotificationHub> hub)
    {
        _svc = svc;
        _hub = hub;
    }

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    // ─── Чаты ─────────────────────────────────────────────────────────────────

    /// <summary>Список всех моих чатов.</summary>
    [HttpGet("api/messages/chats")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatSummaryDto>>> GetMyChats(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetMyChatsAsync(userId.Value, ct));
    }

    /// <summary>Получить или создать личный диалог (DM) с указанным пользователем.</summary>
    [HttpPost("api/messages/direct")]
    [ProducesResponseType(typeof(ChatSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatSummaryDto>> GetOrCreateDirect([FromBody] GetOrCreateDirectRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetOrCreateDirectChatAsync(userId.Value, req.WithUserId, ct));
    }

    /// <summary>Получить список сообщений личного диалога с пользователем.</summary>
    [HttpGet("api/messages/direct/{otherUserId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetDirectMessages(
        Guid otherUserId,
        [FromQuery] int limit = 50,
        [FromQuery] DateTimeOffset? before = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var chat = await _svc.GetOrCreateDirectChatAsync(userId.Value, otherUserId, ct);
        return Ok(await _svc.GetMessagesAsync(chat.Id, userId.Value, limit, before, ct));
    }

    /// <summary>Создать групповой чат.</summary>
    [HttpPost("api/messages/chats")]
    [ProducesResponseType(typeof(ChatSummaryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatSummaryDto>> CreateGroupChat([FromBody] CreateGroupChatRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.CreateGroupChatAsync(userId.Value, req, ct);
        return CreatedAtAction(nameof(GetMessages), new { chatId = dto.Id }, dto);
    }

    /// <summary>Список участников чата.</summary>
    [HttpGet("api/messages/chats/{chatId:guid}/members")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatMemberDto>>> GetChatMembers(Guid chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetChatMembersAsync(chatId, userId.Value, ct));
    }

    /// <summary>Добавить участника в групповой чат.</summary>
    [HttpPost("api/messages/chats/{chatId:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddMember(Guid chatId, [FromBody] Guid memberId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.AddChatMemberAsync(chatId, userId.Value, memberId, ct);
        return NoContent();
    }

    /// <summary>Удалить участника из чата.</summary>
    [HttpDelete("api/messages/chats/{chatId:guid}/members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid chatId, Guid memberId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.RemoveChatMemberAsync(chatId, userId.Value, memberId, ct);
        return NoContent();
    }

    // ─── Сообщения ────────────────────────────────────────────────────────────

    /// <summary>Список сообщений чата.</summary>
    [HttpGet("api/messages/chats/{chatId:guid}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetMessages(
        Guid chatId,
        [FromQuery] int limit = 50,
        [FromQuery] DateTimeOffset? before = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetMessagesAsync(chatId, userId.Value, limit, before, ct));
    }

    /// <summary>Отправить сообщение в чат.</summary>
    [HttpPost("api/messages/chats/{chatId:guid}/messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid chatId, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.SendMessageAsync(chatId, userId.Value, req, ct);

        // SignalR: транслируем новое сообщение в комнату чата
        await _hub.Clients.Group($"chat:{chatId}")
            .SendAsync("bpm:notification", new { type = "ChatMessage", data = dto }, ct);

        return CreatedAtAction(nameof(GetMessages), new { chatId }, dto);
    }

    /// <summary>Редактировать сообщение.</summary>
    [HttpPut("api/messages/{messageId:guid}")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageDto>> EditMessage(Guid messageId, [FromBody] EditMessageRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.EditMessageAsync(messageId, userId.Value, req, ct));
    }

    /// <summary>Удалить сообщение (soft delete).</summary>
    [HttpDelete("api/messages/{messageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMessage(Guid messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.DeleteMessageAsync(messageId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Пометить сообщение прочитанным.</summary>
    [HttpPut("api/messages/{messageId:guid}/read")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkRead(Guid messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var unread = await _svc.MarkReadAsync(messageId, userId.Value, ct);
        return Ok(new { unreadCount = unread });
    }

    /// <summary>Пометить все сообщения чата прочитанными.</summary>
    [HttpPut("api/messages/chats/{chatId:guid}/read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(Guid chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.MarkAllReadAsync(chatId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Поставить или снять реакцию на сообщение.</summary>
    [HttpPost("api/messages/{messageId:guid}/reactions")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageReactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageReactionDto>>> ToggleReaction(
        Guid messageId, [FromBody] ToggleReactionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.ToggleReactionAsync(messageId, userId.Value, req.Emoji, ct));
    }

    /// <summary>Поиск сообщений (в чате или глобально).</summary>
    [HttpGet("api/messages/search")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageSearchResultDto>>> Search(
        [FromQuery] string q, [FromQuery] Guid? chatId = null, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.SearchMessagesAsync(userId.Value, q, chatId, ct));
    }

    // ─── Закреплённые сообщения ───────────────────────────────────────────────

    /// <summary>Получить список закреплённых сообщений в чате.</summary>
    [HttpGet("api/messages/chats/{chatId:guid}/pinned")]
    [ProducesResponseType(typeof(IReadOnlyList<PinnedMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PinnedMessageDto>>> GetPinned(Guid chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetPinnedMessagesAsync(chatId, userId.Value, ct));
    }

    /// <summary>Закрепить сообщение в чате.</summary>
    [HttpPost("api/messages/chats/{chatId:guid}/pinned")]
    [ProducesResponseType(typeof(PinnedMessageDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PinnedMessageDto>> PinMessage(Guid chatId, [FromBody] Guid messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.PinMessageAsync(chatId, messageId, userId.Value, ct);
        return CreatedAtAction(nameof(GetPinned), new { chatId }, dto);
    }

    /// <summary>Открепить сообщение.</summary>
    [HttpDelete("api/messages/chats/{chatId:guid}/pinned/{pinId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnpinMessage(Guid chatId, Guid pinId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.UnpinMessageAsync(pinId, userId.Value, ct);
        return NoContent();
    }

    // ─── Typing indicator (SignalR) ───────────────────────────────────────────

    /// <summary>Отправить индикатор «пользователь печатает» всем участникам чата.</summary>
    [HttpPost("api/messages/chats/{chatId:guid}/typing")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Typing(Guid chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Email) ?? userId.ToString();
        await _hub.Clients.Group($"chat:{chatId}")
            .SendAsync("bpm:notification", new
            {
                type = "Typing",
                data = new { chatId, userId, displayName }
            }, ct);
        return NoContent();
    }

    // ─── Счётчик непрочитанных ────────────────────────────────────────────────

    /// <summary>Суммарное количество непрочитанных сообщений.</summary>
    [HttpGet("api/messages/unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetUnreadCountAsync(userId.Value, ct));
    }

    // ─── Настройки ленты ──────────────────────────────────────────────────────

    /// <summary>Получить настройки отображения ленты сообщений.</summary>
    [HttpGet("api/users/me/messaging-prefs")]
    [ProducesResponseType(typeof(MessagingPrefsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagingPrefsDto>> GetPrefs(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetMessagingPrefsAsync(userId.Value, ct));
    }

    /// <summary>Сохранить настройки отображения ленты сообщений.</summary>
    [HttpPut("api/users/me/messaging-prefs")]
    [ProducesResponseType(typeof(MessagingPrefsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagingPrefsDto>> UpdatePrefs([FromBody] UpdateMessagingPrefsRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.UpdateMessagingPrefsAsync(userId.Value, req, ct));
    }
}
