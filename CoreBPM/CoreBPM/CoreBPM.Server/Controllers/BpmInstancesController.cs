using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления экземплярами бизнес-процессов.</summary>
[ApiController]
[Authorize]
public class BpmInstancesController : ControllerBase
{
    private readonly IBpmInstanceService _service;

    public BpmInstancesController(IBpmInstanceService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список экземпляров процесса.</summary>
    [HttpGet("api/bpm/processes/{processId}/instances")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmInstanceListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmInstanceListItemDto>>> GetByProcess(
        Guid processId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetInstancesAsync(processId, page, pageSize, ct));

    /// <summary>Запускает новый экземпляр процесса вручную.</summary>
    [HttpPost("api/bpm/processes/{processId}/instances")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> Create(
        Guid processId,
        [FromBody] CreateInstanceRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var instance = await _service.CreateInstanceAsync(processId, request, userId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { instanceId = instance.Id }, instance);
    }

    /// <summary>Возвращает экземпляр процесса по идентификатору.</summary>
    [HttpGet("api/bpm/instances/{instanceId}")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> GetById(
        Guid instanceId,
        CancellationToken ct)
        => Ok(await _service.GetInstanceByIdAsync(instanceId, ct));

    /// <summary>Возвращает задания планировщика для процесса (таймерные стартовые события).</summary>
    [HttpGet("api/bpm/processes/{processId}/scheduler-jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmSchedulerJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmSchedulerJobDto>>> GetSchedulerJobs(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetSchedulerJobsAsync(processId, ct));

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
