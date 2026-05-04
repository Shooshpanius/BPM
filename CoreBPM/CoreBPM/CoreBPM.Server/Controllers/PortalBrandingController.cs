using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Portal.DTOs;
using CoreBPM.Server.Application.Portal.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер брендинга портала (FR-PORTAL-01.4).</summary>
[ApiController]
[Route("api/portal/branding")]
[Authorize]
public class PortalBrandingController : ControllerBase
{
    private readonly IPortalBrandingService _svc;
    public PortalBrandingController(IPortalBrandingService svc) { _svc = svc; }

    /// <summary>Возвращает настройки брендинга портала.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PortalBrandingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalBrandingDto>> Get(CancellationToken ct)
        => Ok(await _svc.GetBrandingAsync(ct));

    /// <summary>Обновляет настройки брендинга (только Admin).</summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PortalBrandingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PortalBrandingDto>> Update(
        [FromBody] UpdateBrandingRequest req, CancellationToken ct)
        => Ok(await _svc.UpdateBrandingAsync(req, ct));
}
