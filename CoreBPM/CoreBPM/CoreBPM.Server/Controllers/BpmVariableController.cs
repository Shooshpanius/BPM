using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления переменными контекста бизнес-процесса.</summary>
[ApiController]
[Route("api/bpm/processes/{processId}/variables")]
[Authorize]
public class BpmVariableController : ControllerBase
{
    private readonly IBpmVariableService _service;

    public BpmVariableController(IBpmVariableService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список переменных процесса.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessVariableDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessVariableDto>>> GetAll(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetVariablesAsync(processId, ct));

    /// <summary>Создаёт новую переменную процесса.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BpmProcessVariableDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessVariableDto>> Create(
        Guid processId,
        [FromBody] CreateBpmVariableRequest request,
        CancellationToken ct)
    {
        var dto = await _service.CreateVariableAsync(processId, request, ct);
        return CreatedAtAction(nameof(GetAll), new { processId }, dto);
    }

    /// <summary>Обновляет переменную процесса.</summary>
    [HttpPut("{variableId}")]
    [ProducesResponseType(typeof(BpmProcessVariableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessVariableDto>> Update(
        Guid processId,
        Guid variableId,
        [FromBody] UpdateBpmVariableRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateVariableAsync(processId, variableId, request, ct));

    /// <summary>Удаляет переменную процесса.</summary>
    [HttpDelete("{variableId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid processId,
        Guid variableId,
        CancellationToken ct)
    {
        await _service.DeleteVariableAsync(processId, variableId, ct);
        return NoContent();
    }

    /// <summary>Изменяет порядок переменных процесса.</summary>
    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reorder(
        Guid processId,
        [FromBody] ReorderVariablesRequest request,
        CancellationToken ct)
    {
        await _service.ReorderVariablesAsync(processId, request.OrderedIds, ct);
        return NoContent();
    }
}
