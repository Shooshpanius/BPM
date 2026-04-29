using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// Контроллер визуализации оргструктуры (FR-ORG-01.4).
/// Публичный вид доступен всем авторизованным пользователям;
/// расширенный вид (ставки, вакансии) — только Admin и HR.
/// </summary>
[ApiController]
[Route("api/org/chart")]
[Authorize]
public class OrgChartController : ControllerBase
{
    private readonly IOrgChartService _service;

    public OrgChartController(IOrgChartService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает дерево оргструктуры организации с сотрудниками.
    /// </summary>
    /// <param name="organizationId">Идентификатор организации (обязательный).</param>
    /// <param name="search">Текстовый поиск по имени, должности или подразделению.</param>
    /// <param name="extended">
    /// При true — включает данные о ставках и вакансиях.
    /// Игнорируется, если текущий пользователь не имеет роли Admin или HR.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(OrgChartDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrgChartDto>> GetChart(
        [FromQuery] Guid organizationId,
        [FromQuery] string? search,
        [FromQuery] bool extended,
        CancellationToken ct)
    {
        if (organizationId == Guid.Empty)
            return BadRequest(new { error = "Параметр organizationId обязателен" });

        // Расширенный вид доступен только Admin и HR
        var canExtended = User.IsInRole("Admin") || User.IsInRole("HR");
        var result = await _service.GetChartAsync(organizationId, search, extended && canExtended, ct);
        return Ok(result);
    }
}
