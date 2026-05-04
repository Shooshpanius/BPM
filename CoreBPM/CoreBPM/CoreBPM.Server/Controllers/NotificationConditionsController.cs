using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Настройки режима «Не беспокоить» и журнал доставки уведомлений (FR-MSG-02.2).</summary>
[ApiController]
[Authorize]
public class NotificationConditionsController : ControllerBase
{
    private readonly INotificationSettingsService _service;

    public NotificationConditionsController(INotificationSettingsService service)
        => _service = service;

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    // ── DND ──────────────────────────────────────────────────────────────────

    /// <summary>Получить настройки режима «Не беспокоить» текущего пользователя. FR-MSG-02.2.</summary>
    [HttpGet("api/users/me/notification-settings/dnd")]
    [ProducesResponseType(typeof(DndSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DndSettingsDto>> GetDnd(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetDndSettingsAsync(userId.Value, ct));
    }

    /// <summary>Обновить настройки режима «Не беспокоить». FR-MSG-02.2.</summary>
    [HttpPut("api/users/me/notification-settings/dnd")]
    [ProducesResponseType(typeof(DndSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DndSettingsDto>> UpdateDnd(
        [FromBody] DndSettingsDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateDndSettingsAsync(userId.Value, dto, ct));
    }

    // ── Журнал доставки (только администратор) ────────────────────────────────

    /// <summary>Журнал доставки уведомлений (администратор). FR-MSG-02.2.</summary>
    [HttpGet("api/admin/notification-logs")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryLogs(
        [FromQuery] Guid? userId,
        [FromQuery] string? type,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var filter = new DeliveryLogFilterRequest
        {
            UserId = userId,
            EventType = type,
            Channel = channel,
            Status = status,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize,
        };
        var (items, total) = await _service.GetDeliveryLogsAsync(filter, ct);
        return Ok(new { items, total, page, pageSize });
    }
}
