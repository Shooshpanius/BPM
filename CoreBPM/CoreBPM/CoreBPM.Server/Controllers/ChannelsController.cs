using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API информационных каналов (FR-MSG-01.2).</summary>
[ApiController]
[Authorize]
public class ChannelsController : ControllerBase
{
    private readonly IMessagingService _svc;

    public ChannelsController(IMessagingService svc) => _svc = svc;

    private Guid? GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Список всех доступных каналов с количеством подписчиков.</summary>
    [HttpGet("api/messages/channels")]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChannelSummaryDto>>> GetChannels(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetChannelsAsync(userId.Value, ct));
    }

    /// <summary>Создать информационный канал.</summary>
    [HttpPost("api/messages/channels")]
    [ProducesResponseType(typeof(ChannelSummaryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChannelSummaryDto>> CreateChannel([FromBody] CreateChannelRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.CreateChannelAsync(userId.Value, req, ct);
        return CreatedAtAction(nameof(GetChannels), dto);
    }

    /// <summary>Редактировать информационный канал (только администратор).</summary>
    [HttpPut("api/messages/channels/{channelId:guid}")]
    [ProducesResponseType(typeof(ChannelSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelSummaryDto>> UpdateChannel(Guid channelId, [FromBody] UpdateChannelRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.UpdateChannelAsync(channelId, userId.Value, req, ct));
    }

    /// <summary>Удалить информационный канал (только администратор/создатель).</summary>
    [HttpDelete("api/messages/channels/{channelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteChannel(Guid channelId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.DeleteChannelAsync(channelId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Подписаться на канал.</summary>
    [HttpPost("api/messages/channels/{channelId:guid}/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Subscribe(Guid channelId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.SubscribeAsync(channelId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Отписаться от канала.</summary>
    [HttpDelete("api/messages/channels/{channelId:guid}/subscribe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsubscribe(Guid channelId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.UnsubscribeAsync(channelId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Список публикаций канала.</summary>
    [HttpGet("api/messages/channels/{channelId:guid}/posts")]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelPostDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChannelPostDto>>> GetPosts(
        Guid channelId,
        [FromQuery] int limit = 30,
        [FromQuery] DateTimeOffset? before = null,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetPostsAsync(channelId, userId.Value, limit, before, q, ct));
    }

    /// <summary>Создать публикацию в канале (только администратор/модератор).</summary>
    [HttpPost("api/messages/channels/{channelId:guid}/posts")]
    [ProducesResponseType(typeof(ChannelPostDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChannelPostDto>> CreatePost(Guid channelId, [FromBody] CreateChannelPostRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.CreatePostAsync(channelId, userId.Value, req, ct);
        return CreatedAtAction(nameof(GetPosts), new { channelId }, dto);
    }

    /// <summary>Редактировать публикацию.</summary>
    [HttpPut("api/messages/channels/{channelId:guid}/posts/{postId:guid}")]
    [ProducesResponseType(typeof(ChannelPostDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChannelPostDto>> EditPost(Guid channelId, Guid postId, [FromBody] EditChannelPostRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.EditPostAsync(postId, userId.Value, req, ct));
    }

    /// <summary>Удалить публикацию.</summary>
    [HttpDelete("api/messages/channels/{channelId:guid}/posts/{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePost(Guid channelId, Guid postId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.DeletePostAsync(postId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Добавить или снять реакцию на публикацию.</summary>
    [HttpPost("api/messages/channels/{channelId:guid}/posts/{postId:guid}/react")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageReactionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageReactionDto>>> TogglePostReaction(
        Guid channelId, Guid postId, [FromBody] ToggleReactionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.TogglePostReactionAsync(postId, userId.Value, req.Emoji, ct));
    }

    /// <summary>Получить комментарии к публикации.</summary>
    [HttpGet("api/messages/channels/{channelId:guid}/posts/{postId:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<PostCommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PostCommentDto>>> GetPostComments(Guid channelId, Guid postId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetPostCommentsAsync(postId, userId.Value, ct));
    }

    /// <summary>Добавить комментарий к публикации.</summary>
    [HttpPost("api/messages/channels/{channelId:guid}/posts/{postId:guid}/comments")]
    [ProducesResponseType(typeof(PostCommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PostCommentDto>> AddPostComment(Guid channelId, Guid postId, [FromBody] AddPostCommentRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.AddPostCommentAsync(postId, userId.Value, req, ct);
        return CreatedAtAction(nameof(GetPostComments), new { channelId, postId }, dto);
    }

    /// <summary>Удалить комментарий к публикации.</summary>
    [HttpDelete("api/messages/channels/{channelId:guid}/posts/{postId:guid}/comments/{commentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePostComment(Guid channelId, Guid postId, Guid commentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.DeletePostCommentAsync(commentId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Список подписчиков канала.</summary>
    [HttpGet("api/messages/channels/{channelId:guid}/subscribers")]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelSubscriberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChannelSubscriberDto>>> GetSubscribers(Guid channelId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetSubscribersAsync(channelId, userId.Value, ct));
    }

    /// <summary>Назначить или снять роль администратора у подписчика.</summary>
    [HttpPut("api/messages/channels/{channelId:guid}/subscribers/{targetUserId:guid}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetSubscriberRole(Guid channelId, Guid targetUserId, [FromBody] SetSubscriberRoleRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.SetSubscriberRoleAsync(channelId, userId.Value, targetUserId, req.IsAdmin, ct);
        return NoContent();
    }

    /// <summary>Пригласить пользователя в приватный канал (только администратор).</summary>
    [HttpPost("api/messages/channels/{channelId:guid}/invite")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> InviteToChannel(Guid channelId, [FromBody] InviteToChannelRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.InviteToChannelAsync(channelId, userId.Value, req.UserId, ct);
        return NoContent();
    }

    /// <summary>Список закреплённых публикаций канала.</summary>
    [HttpGet("api/messages/channels/{channelId:guid}/pinned-posts")]
    [ProducesResponseType(typeof(IReadOnlyList<ChannelPinnedPostDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChannelPinnedPostDto>>> GetPinnedPosts(Guid channelId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetPinnedPostsAsync(channelId, userId.Value, ct));
    }

    /// <summary>Закрепить публикацию в канале (только администратор).</summary>
    [HttpPost("api/messages/channels/{channelId:guid}/posts/{postId:guid}/pin")]
    [ProducesResponseType(typeof(ChannelPinnedPostDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChannelPinnedPostDto>> PinPost(Guid channelId, Guid postId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _svc.PinPostAsync(channelId, postId, userId.Value, ct);
        return CreatedAtAction(nameof(GetPinnedPosts), new { channelId }, dto);
    }

    /// <summary>Открепить публикацию канала.</summary>
    [HttpDelete("api/messages/channels/{channelId:guid}/posts/{postId:guid}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnpinPost(Guid channelId, Guid postId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _svc.UnpinPostAsync(channelId, postId, userId.Value, ct);
        return NoContent();
    }
}
