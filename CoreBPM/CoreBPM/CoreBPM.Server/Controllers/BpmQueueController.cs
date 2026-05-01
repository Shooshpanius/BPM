using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Controllers;

/// <summary>API очереди исполнения и аналитики узлов (FR-BPM-02.5).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class BpmQueueController : ControllerBase
{
    private readonly IBpmQueueService _service;

    public BpmQueueController(IBpmQueueService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список заданий в очереди исполнения.</summary>
    [HttpGet("api/admin/queue")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmExecutionJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmExecutionJobDto>>> GetQueue(
        [FromQuery] BpmJobStatus? status,
        [FromQuery] string? instanceName,
        [FromQuery] Guid? processId,
        [FromQuery] bool includeScheduled = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        return Ok(await _service.GetQueueAsync(status, instanceName, processId, includeScheduled, page, pageSize, ct));
    }

    /// <summary>Возвращает агрегированные счётчики по статусам очереди.</summary>
    [HttpGet("api/admin/queue/stats")]
    [ProducesResponseType(typeof(QueueStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<QueueStatsDto>> GetStats(CancellationToken ct)
        => Ok(await _service.GetQueueStatsAsync(ct));

    /// <summary>Принудительно повторяет задание (сбрасывает статус на Pending).</summary>
    [HttpPost("api/admin/queue/{jobId}/retry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryJob(Guid jobId, CancellationToken ct)
    {
        await _service.RetryJobAsync(jobId, ct);
        return NoContent();
    }

    /// <summary>Отменяет таймерное задание.</summary>
    [HttpPost("api/admin/queue/{jobId}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelTimer(Guid jobId, CancellationToken ct)
    {
        await _service.CancelTimerAsync(jobId, ct);
        return NoContent();
    }

    /// <summary>Переносит время запуска таймерного задания.</summary>
    [HttpPut("api/admin/queue/{jobId}/reschedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RescheduleTimer(
        Guid jobId,
        [FromBody] RescheduleTimerRequest request,
        CancellationToken ct)
    {
        await _service.RescheduleTimerAsync(jobId, request.NewRunAt, ct);
        return NoContent();
    }
}

/// <summary>API аналитики узлов процесса.</summary>
[ApiController]
[Authorize]
public class BpmAnalyticsController : ControllerBase
{
    private readonly IBpmQueueService _service;

    public BpmAnalyticsController(IBpmQueueService service)
    {
        _service = service;
    }

    /// <summary>Возвращает аналитику выполнения узлов процесса (среднее и перцентили по времени).</summary>
    [HttpGet("api/analytics/nodes")]
    [ProducesResponseType(typeof(IReadOnlyList<NodeAnalyticsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NodeAnalyticsDto>>> GetNodeAnalytics(
        [FromQuery] Guid processId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetNodeAnalyticsAsync(processId, from, to, ct));
}
