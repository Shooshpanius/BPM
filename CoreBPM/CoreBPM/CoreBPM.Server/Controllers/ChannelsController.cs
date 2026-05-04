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
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _svc.GetPostsAsync(channelId, userId.Value, limit, before, ct));
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
}
