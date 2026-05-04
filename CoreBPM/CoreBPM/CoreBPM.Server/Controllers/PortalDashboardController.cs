using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Portal.DTOs;
using CoreBPM.Server.Application.Portal.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер дашборда портала (FR-PORTAL-01.1).</summary>
[ApiController]
[Route("api/portal/dashboard")]
[Authorize]
public class PortalDashboardController : ControllerBase
{
    private readonly IPortalDashboardService _svc;
    public PortalDashboardController(IPortalDashboardService svc) { _svc = svc; }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Возвращает конфигурацию дашборда текущего пользователя.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PortalDashboardWidgetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalDashboardWidgetDto>>> Get(CancellationToken ct)
        => Ok(await _svc.GetDashboardAsync(UserId, ct));

    /// <summary>Полная замена конфигурации дашборда (сохранение).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<PortalDashboardWidgetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalDashboardWidgetDto>>> Save(
        [FromBody] SaveDashboardRequest req, CancellationToken ct)
        => Ok(await _svc.SaveDashboardAsync(UserId, req, ct));

    /// <summary>Добавляет новый виджет на дашборд.</summary>
    [HttpPost("widgets")]
    [ProducesResponseType(typeof(PortalDashboardWidgetDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalDashboardWidgetDto>> AddWidget(
        [FromBody] AddWidgetRequest req, CancellationToken ct)
    {
        var dto = await _svc.AddWidgetAsync(UserId, req, ct);
        return CreatedAtAction(nameof(Get), dto);
    }

    /// <summary>Обновляет позицию/размер/настройки виджета.</summary>
    [HttpPut("widgets/{widgetId:guid}")]
    [ProducesResponseType(typeof(PortalDashboardWidgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalDashboardWidgetDto>> UpdateWidget(
        Guid widgetId, [FromBody] UpdateWidgetRequest req, CancellationToken ct)
    {
        try { return Ok(await _svc.UpdateWidgetAsync(UserId, widgetId, req, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Удаляет виджет с дашборда.</summary>
    [HttpDelete("widgets/{widgetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWidget(Guid widgetId, CancellationToken ct)
    {
        try { await _svc.DeleteWidgetAsync(UserId, widgetId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Сбрасывает дашборд к шаблону по умолчанию.</summary>
    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _svc.ResetToDefaultAsync(UserId, ct);
        return NoContent();
    }
}
