using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// Настройки условий отправки уведомлений: DND, throttle, журнал доставки,
/// retention, статистика (FR-MSG-02.2).
/// </summary>
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

    // ── Throttle (ограничение частоты) ────────────────────────────────────

    /// <summary>
    /// Получить настройки ограничения частоты уведомлений текущего пользователя. FR-MSG-02.2.
    /// </summary>
    [HttpGet("api/users/me/notification-settings/throttle")]
    [ProducesResponseType(typeof(IReadOnlyList<ThrottleSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ThrottleSettingDto>>> GetThrottle(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetThrottleSettingsAsync(userId.Value, ct));
    }

    /// <summary>
    /// Обновить настройки ограничения частоты уведомлений. FR-MSG-02.2.
    /// Позволяет задать минимальный интервал (в минутах) между уведомлениями
    /// одного типа по одному каналу. 0 = без ограничения.
    /// </summary>
    [HttpPut("api/users/me/notification-settings/throttle")]
    [ProducesResponseType(typeof(IReadOnlyList<ThrottleSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ThrottleSettingDto>>> UpdateThrottle(
        [FromBody] UpdateThrottleSettingsRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateThrottleSettingsAsync(userId.Value, request, ct));
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

    /// <summary>
    /// Экспорт журнала доставки уведомлений в CSV (администратор). FR-MSG-02.2.
    /// Поддерживает те же фильтры, что и GET /api/admin/notification-logs.
    /// </summary>
    [HttpGet("api/admin/notification-logs/export")]
    [Authorize(Roles = "Admin")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDeliveryLogs(
        [FromQuery] Guid? userId,
        [FromQuery] string? type,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        // Получаем все записи без пагинации (максимум 10000 строк)
        var filter = new DeliveryLogFilterRequest
        {
            UserId = userId,
            EventType = type,
            Channel = channel,
            Status = status,
            From = from,
            To = to,
            Page = 1,
            PageSize = 10000,
        };
        var (items, _) = await _service.GetDeliveryLogsAsync(filter, ct);

        var sb = new StringBuilder();
        sb.AppendLine("ID,Пользователь,Тип события,Канал,Статус,Ошибка,Дата");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                item.Id,
                CsvEscape(item.UserFullName),
                CsvEscape(item.EventType),
                CsvEscape(item.Channel),
                CsvEscape(item.Status),
                CsvEscape(item.Error?.Replace("\"", "'")),
                item.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"notification-logs-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    // ── Вспомогательные методы ────────────────────────────────────────────────

    /// <summary>
    /// Экранирует значение для CSV: оборачивает в кавычки и предотвращает Formula Injection.
    /// Значения, начинающиеся с =, +, -, @, \t, \r — потенциальные формулы в электронных таблицах.
    /// </summary>
    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        // Нейтрализуем CSV/Formula Injection: символы =, +, -, @, TAB, CR в начале строки
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ── Retention (хранение журнала, только администратор) ────────────────────

    /// <summary>
    /// Получить настройки срока хранения журнала доставки уведомлений. FR-MSG-02.2.
    /// </summary>
    [HttpGet("api/admin/notification-settings/retention")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(NotificationLogRetentionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationLogRetentionDto>> GetRetention(CancellationToken ct)
        => Ok(await _service.GetRetentionSettingsAsync(ct));

    /// <summary>
    /// Обновить настройки срока хранения журнала доставки уведомлений. FR-MSG-02.2.
    /// RetentionDays = 0 означает «хранить бессрочно».
    /// </summary>
    [HttpPut("api/admin/notification-settings/retention")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(NotificationLogRetentionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationLogRetentionDto>> UpdateRetention(
        [FromBody] NotificationLogRetentionDto dto, CancellationToken ct)
        => Ok(await _service.UpdateRetentionSettingsAsync(dto, ct));

    // ── Статистика доставки (только администратор) ─────────────────────────

    /// <summary>
    /// Сводная статистика доставки уведомлений по каналам и типам событий (администратор). FR-MSG-02.2.
    /// </summary>
    [HttpGet("api/admin/notification-stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(DeliveryStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DeliveryStatsDto>> GetStats(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetDeliveryStatsAsync(from, to, ct));
}

