using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API in-app уведомлений пользователя (FR-MSG-02.1).</summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IInAppNotificationService _svc;

    public NotificationsController(IInAppNotificationService svc) => _svc = svc;

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }

    /// <summary>Возвращает список уведомлений текущего пользователя.</summary>
    /// <param name="read">true — только прочитанные; false — только непрочитанные; не задан — все.</param>
    /// <param name="type">Фильтр по типу события.</param>
    /// <param name="page">Страница (начинается с 1).</param>
    /// <param name="pageSize">Размер страницы (по умолчанию 20).</param>
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] bool? read,
        [FromQuery] string? type,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _svc.GetPagedAsync(userId, read, type, page, pageSize, ct);
        return Ok(new { items, total, page, pageSize });
    }

    /// <summary>Возвращает количество непрочитанных уведомлений.</summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        var count = await _svc.GetUnreadCountAsync(userId, ct);
        return Ok(new { count });
    }

    /// <summary>Отмечает уведомление прочитанным.</summary>
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _svc.MarkReadAsync(userId, id, ct);
        return NoContent();
    }

    /// <summary>Отмечает все уведомления пользователя прочитанными.</summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _svc.MarkAllReadAsync(userId, ct);
        return NoContent();
    }

    /// <summary>Удаляет уведомление.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty) return Unauthorized();

        await _svc.DeleteAsync(userId, id, ct);
        return NoContent();
    }
}
