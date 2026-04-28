using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер управления деревом подразделений.</summary>
[ApiController]
[Route("api/org/units")]
[Authorize]
public class OrgUnitsController : ControllerBase
{
    private readonly IOrgUnitsService _service;

    public OrgUnitsController(IOrgUnitsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает дерево подразделений организации.
    /// Доступно всем авторизованным пользователям.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrgUnitTreeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrgUnitTreeDto>>> GetTree(
        [FromQuery] Guid organizationId,
        [FromQuery] DepartmentStatus? status,
        [FromQuery] string? search,
        CancellationToken ct)
        => Ok(await _service.GetTreeAsync(organizationId, status, search, ct));

    /// <summary>
    /// Возвращает подробную карточку подразделения с breadcrumb и счётчиками.
    /// Доступно всем авторизованным пользователям.
    /// </summary>
    [HttpGet("{unitId:guid}")]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitDto>> GetById(Guid unitId, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(unitId, ct));

    /// <summary>
    /// Создаёт новое подразделение.
    /// Требует роль Admin или HR.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitDto>> Create(
        [FromBody] CreateUnitRequest request,
        CancellationToken ct)
    {
        var callerId = GetCallerId();
        var result = await _service.CreateAsync(request, callerId, ct);
        return CreatedAtAction(nameof(GetById), new { unitId = result.Id }, result);
    }

    /// <summary>
    /// Обновляет данные подразделения.
    /// Требует роль Admin или HR.
    /// </summary>
    [HttpPut("{unitId:guid}")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitDto>> Update(
        Guid unitId,
        [FromBody] UpdateUnitRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(unitId, request, GetCallerId(), ct));

    /// <summary>
    /// Архивирует подразделение (мягкое удаление).
    /// Требует роль Admin или HR.
    /// </summary>
    [HttpDelete("{unitId:guid}")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid unitId, CancellationToken ct)
    {
        await _service.ArchiveAsync(unitId, GetCallerId(), ct);
        return NoContent();
    }

    /// <summary>
    /// Перемещает подразделение в новый родительский узел (с пересчётом путей потомков).
    /// Требует роль Admin или HR.
    /// </summary>
    [HttpPut("{unitId:guid}/move")]
    [Authorize(Roles = "Admin,HR")]
    [ProducesResponseType(typeof(OrgUnitDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrgUnitDto>> Move(
        Guid unitId,
        [FromBody] MoveUnitRequest request,
        CancellationToken ct)
        => Ok(await _service.MoveAsync(unitId, request, GetCallerId(), ct));

    /// <summary>
    /// Возвращает историю изменений подразделения.
    /// Доступно всем авторизованным пользователям.
    /// </summary>
    [HttpGet("{unitId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<UnitHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<UnitHistoryDto>>> GetHistory(Guid unitId, CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(unitId, ct));

    private Guid? GetCallerId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
