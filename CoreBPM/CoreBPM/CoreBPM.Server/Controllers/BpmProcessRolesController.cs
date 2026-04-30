using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления ролями (Владелец/Куратор) в определении бизнес-процесса.</summary>
[ApiController]
[Route("api/bpm/processes/{processId}/roles")]
[Authorize]
public class BpmProcessRolesController : ControllerBase
{
    private readonly IBpmProcessRoleService _service;

    public BpmProcessRolesController(IBpmProcessRoleService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список ролей (Владелец, Кураторы) для указанного процесса.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessRoleConfigDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessRoleConfigDto>>> GetAll(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetRolesAsync(processId, ct));

    /// <summary>Полностью заменяет набор ролей процесса (Владелец и Кураторы).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessRoleConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessRoleConfigDto>>> Replace(
        Guid processId,
        [FromBody] UpsertProcessRoleConfigsRequest request,
        CancellationToken ct)
        => Ok(await _service.ReplaceRolesAsync(processId, request, ct));
}
