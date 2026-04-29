using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления RACI-матрицей ответственности бизнес-процесса.</summary>
[ApiController]
[Route("api/bpm/processes/{processId}/raci")]
[Authorize]
public class BpmRaciController : ControllerBase
{
    private readonly IBpmRaciService _service;

    public BpmRaciController(IBpmRaciService service)
    {
        _service = service;
    }

    /// <summary>Возвращает RACI-матрицу процесса.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmRaciEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmRaciEntryDto>>> GetAll(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetEntriesAsync(processId, ct));

    /// <summary>Полностью заменяет RACI-матрицу процесса.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(IReadOnlyList<BpmRaciEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BpmRaciEntryDto>>> Replace(
        Guid processId,
        [FromBody] IReadOnlyList<UpsertRaciEntryRequest> entries,
        CancellationToken ct)
        => Ok(await _service.ReplaceEntriesAsync(processId, entries, ct));
}
