using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Portal.DTOs;
using CoreBPM.Server.Application.Portal.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер навигационного меню портала (FR-PORTAL-01.4).</summary>
[ApiController]
[Route("api/portal/menu")]
[Authorize]
public class PortalMenuController : ControllerBase
{
    private readonly IPortalMenuService _svc;
    public PortalMenuController(IPortalMenuService svc) { _svc = svc; }

    /// <summary>Возвращает структуру меню для текущего пользователя.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PortalMenuItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalMenuItemDto>>> Get(CancellationToken ct)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        return Ok(await _svc.GetMenuAsync(role, ct));
    }

    /// <summary>Сохраняет структуру меню (только Admin).</summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<PortalMenuItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PortalMenuItemDto>>> Save(
        [FromBody] SaveMenuRequest req, CancellationToken ct)
        => Ok(await _svc.SaveMenuAsync(req, ct));
}
