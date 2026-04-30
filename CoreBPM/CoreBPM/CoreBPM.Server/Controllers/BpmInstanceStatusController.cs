using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления пользовательскими статусами экземпляров бизнес-процесса (FR-BPM-01.6).</summary>
[ApiController]
[Route("api/bpm/processes/{processId}/status-config")]
[Authorize]
public class BpmInstanceStatusController : ControllerBase
{
    private readonly IBpmInstanceStatusService _service;

    public BpmInstanceStatusController(IBpmInstanceStatusService service)
    {
        _service = service;
    }

    /// <summary>Возвращает конфигурацию статусов процесса (привязанная переменная, действие при прерывании, список вариантов).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(InstanceStatusConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstanceStatusConfigDto>> GetConfig(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetConfigAsync(processId, ct));

    /// <summary>
    /// Обновляет конфигурацию статусов.
    /// При CreateVariable = true — создаёт новую переменную типа List и привязывает её к конфигу.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(InstanceStatusConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstanceStatusConfigDto>> UpdateConfig(
        Guid processId,
        [FromBody] UpdateStatusConfigRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateConfigAsync(processId, request, ct));

    /// <summary>Добавляет новый вариант статуса. Если Code не передан — генерируется автоматически транслитерацией из Name.</summary>
    [HttpPost("options")]
    [ProducesResponseType(typeof(InstanceStatusOptionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstanceStatusOptionDto>> CreateOption(
        Guid processId,
        [FromBody] CreateStatusOptionRequest request,
        CancellationToken ct)
    {
        var dto = await _service.CreateOptionAsync(processId, request, ct);
        return CreatedAtAction(nameof(GetConfig), new { processId }, dto);
    }

    /// <summary>Обновляет название и код варианта статуса.</summary>
    [HttpPut("options/{optionId}")]
    [ProducesResponseType(typeof(InstanceStatusOptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstanceStatusOptionDto>> UpdateOption(
        Guid processId,
        Guid optionId,
        [FromBody] UpdateStatusOptionRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateOptionAsync(processId, optionId, request, ct));

    /// <summary>Удаляет вариант статуса.</summary>
    [HttpDelete("options/{optionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOption(
        Guid processId,
        Guid optionId,
        CancellationToken ct)
    {
        await _service.DeleteOptionAsync(processId, optionId, ct);
        return NoContent();
    }

    /// <summary>Изменяет порядок вариантов статусов (batch reorder).</summary>
    [HttpPut("options/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderOptions(
        Guid processId,
        [FromBody] ReorderStatusOptionsRequest request,
        CancellationToken ct)
    {
        await _service.ReorderOptionsAsync(processId, request.OrderedIds, ct);
        return NoContent();
    }
}
