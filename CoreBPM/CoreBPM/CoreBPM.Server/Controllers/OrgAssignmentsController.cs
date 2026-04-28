using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// Публичный контроллер для просмотра назначений пользователей на должности.
/// Доступен всем авторизованным пользователям.
/// </summary>
[ApiController]
[Route("api/org/assignments")]
[Authorize]
public class OrgAssignmentsController : ControllerBase
{
    private readonly IOrgAssignmentService _service;

    public OrgAssignmentsController(IOrgAssignmentService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает список назначений с опциональной фильтрацией.
    /// </summary>
    /// <param name="userId">Фильтр по пользователю.</param>
    /// <param name="positionId">Фильтр по должности.</param>
    /// <param name="organizationId">Фильтр по организации.</param>
    /// <param name="activeOnly">Если true — только активные назначения.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AssignmentDto>>> GetAll(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? positionId,
        [FromQuery] Guid? organizationId,
        [FromQuery] bool? activeOnly,
        CancellationToken ct)
        => Ok(await _service.GetAllAsync(userId, positionId, organizationId, activeOnly, ct));

    /// <summary>Возвращает назначение по идентификатору.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));
}
