using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для работы с диаграммой BPMN 2.0 конкретного процесса.</summary>
[ApiController]
[Route("api/bpm/processes/{processId:guid}/diagram")]
[Authorize]
public class BpmDiagramController : ControllerBase
{
    private readonly IBpmProcessService _service;

    public BpmDiagramController(IBpmProcessService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает текущую диаграмму процесса (последний черновик или активную версию).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BpmDiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDiagramDto>> Get(Guid processId, CancellationToken ct)
        => Ok(await _service.GetDiagramAsync(processId, ct));

    /// <summary>
    /// Сохраняет XML-диаграмму BPMN 2.0 в текущий черновик процесса.
    /// Если черновика нет — создаётся новая версия.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(BpmDiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDiagramDto>> Save(
        Guid processId,
        [FromBody] SaveDiagramRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя");

        return Ok(await _service.SaveDiagramAsync(processId, request, userId, ct));
    }

    // ─── Вспомогательные методы ───

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
