using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления бизнес-процессами (CRUD).</summary>
[ApiController]
[Route("api/bpm/processes")]
[Authorize]
public class BpmProcessesController : ControllerBase
{
    private readonly IBpmProcessService _service;

    public BpmProcessesController(IBpmProcessService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список процессов организации.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessListItemDto>>> GetAll(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.GetProcessesAsync(organizationId, ct));

    /// <summary>Возвращает процесс по идентификатору.</summary>
    [HttpGet("{processId:guid}")]
    [ProducesResponseType(typeof(BpmProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessDto>> GetById(Guid processId, CancellationToken ct)
        => Ok(await _service.GetProcessByIdAsync(processId, ct));

    /// <summary>Создаёт новый процесс.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BpmProcessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BpmProcessDto>> Create(
        [FromBody] CreateBpmProcessRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя");

        var result = await _service.CreateProcessAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { processId = result.Id }, result);
    }

    /// <summary>Обновляет метаданные процесса (название, описание).</summary>
    [HttpPut("{processId:guid}")]
    [ProducesResponseType(typeof(BpmProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessDto>> Update(
        Guid processId,
        [FromBody] UpdateBpmProcessRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateProcessAsync(processId, request, ct));

    /// <summary>Удаляет процесс (мягкое удаление).</summary>
    [HttpDelete("{processId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid processId, CancellationToken ct)
    {
        await _service.DeleteProcessAsync(processId, ct);
        return NoContent();
    }

    /// <summary>Возвращает список версий процесса.</summary>
    [HttpGet("{processId:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessVersionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessVersionInfoDto>>> GetVersions(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetVersionsAsync(processId, ct));

    // ─── Вспомогательные методы ───

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
