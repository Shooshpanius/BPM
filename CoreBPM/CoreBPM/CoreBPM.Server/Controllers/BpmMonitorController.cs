using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API монитора бизнес-процессов (FR-BPM-02.4).</summary>
[ApiController]
[Authorize]
public class BpmMonitorController : ControllerBase
{
    private readonly IBpmMonitorService _service;

    public BpmMonitorController(IBpmMonitorService service)
    {
        _service = service;
    }

    /// <summary>Возвращает процессы, доступные текущему пользователю для мониторинга (Владелец/Куратор), со статистикой экземпляров.</summary>
    [HttpGet("api/bpm/monitor/my")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessMonitorItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessMonitorItemDto>>> GetMy(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetMyMonitorProcessesAsync(userId.Value, ct));
    }

    /// <summary>Возвращает все процессы системы со статистикой экземпляров. Только для администраторов.</summary>
    [HttpGet("api/bpm/monitor/full")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessMonitorItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessMonitorItemDto>>> GetFull(CancellationToken ct)
        => Ok(await _service.GetFullMonitorProcessesAsync(ct));

    /// <summary>Возвращает детальную статистику экземпляров для конкретного процесса.</summary>
    [HttpGet("api/bpm/processes/{processId}/stats")]
    [ProducesResponseType(typeof(BpmProcessStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessStatsDto>> GetProcessStats(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetProcessStatsAsync(processId, ct));

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
