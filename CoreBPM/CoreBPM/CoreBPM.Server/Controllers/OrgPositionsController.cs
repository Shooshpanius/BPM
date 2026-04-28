using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер публичного доступа к должностям (чтение).</summary>
[ApiController]
[Route("api/org/positions")]
[Authorize]
public class OrgPositionsController : ControllerBase
{
    private readonly IOrgPositionsService _service;

    public OrgPositionsController(IOrgPositionsService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает список должностей с опциональной фильтрацией.
    /// Доступно всем авторизованным пользователям.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PositionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PositionResponse>>> GetAll(
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? organizationId,
        [FromQuery] PositionCategory? category,
        [FromQuery] PositionStatus? status,
        CancellationToken ct)
        => Ok(await _service.GetPositionsAsync(departmentId, organizationId, category, status, ct));

    /// <summary>
    /// Возвращает должность по идентификатору.
    /// Доступно всем авторизованным пользователям.
    /// </summary>
    [HttpGet("{positionId:guid}")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionResponse>> GetById(Guid positionId, CancellationToken ct)
        => Ok(await _service.GetPositionByIdAsync(positionId, ct));
}
