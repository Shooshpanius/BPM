using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для работы с конфигурациями элементов BPMN-диаграммы.</summary>
[ApiController]
[Route("api/bpm/processes/{processId}/element-configs")]
[Authorize]
public class BpmElementConfigController : ControllerBase
{
    private readonly IBpmElementConfigService _service;

    public BpmElementConfigController(IBpmElementConfigService service)
    {
        _service = service;
    }

    /// <summary>Возвращает все конфигурации элементов процесса.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmElementConfigDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmElementConfigDto>>> GetAll(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetConfigsAsync(processId, ct));

    /// <summary>Возвращает конфигурацию конкретного элемента.</summary>
    [HttpGet("{elementId}")]
    [ProducesResponseType(typeof(BpmElementConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmElementConfigDto>> GetOne(
        Guid processId,
        string elementId,
        CancellationToken ct)
    {
        var dto = await _service.GetConfigAsync(processId, elementId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Создаёт или обновляет конфигурацию элемента.</summary>
    [HttpPut("{elementId}")]
    [ProducesResponseType(typeof(BpmElementConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmElementConfigDto>> Upsert(
        Guid processId,
        string elementId,
        [FromBody] UpsertElementConfigRequest request,
        CancellationToken ct)
        => Ok(await _service.UpsertConfigAsync(processId, elementId, request, ct));

    /// <summary>Удаляет конфигурацию элемента.</summary>
    [HttpDelete("{elementId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid processId,
        string elementId,
        CancellationToken ct)
    {
        await _service.DeleteConfigAsync(processId, elementId, ct);
        return NoContent();
    }
}
