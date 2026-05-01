using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API аналитики и KPI бизнес-процессов (FR-BPM-03.2).</summary>
[ApiController]
[Authorize]
public class BpmProcessAnalyticsController : ControllerBase
{
    private readonly IBpmAnalyticsService _service;

    public BpmProcessAnalyticsController(IBpmAnalyticsService service)
    {
        _service = service;
    }

    /// <summary>Возвращает агрегированную аналитику процесса за период.</summary>
    [HttpGet("api/analytics/processes/{processId:guid}")]
    [ProducesResponseType(typeof(ProcessAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcessAnalyticsDto>> GetProcessAnalytics(
        Guid processId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetProcessAnalyticsAsync(processId, from, to, ct));

    /// <summary>Возвращает тепловую карту узлов процесса.</summary>
    [HttpGet("api/analytics/processes/{processId:guid}/heatmap")]
    [ProducesResponseType(typeof(IReadOnlyList<NodeHeatMapDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<NodeHeatMapDto>>> GetHeatMap(
        Guid processId,
        [FromQuery] Guid? versionId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetNodeHeatMapAsync(processId, versionId, from, to, ct));

    /// <summary>Возвращает воронку процесса.</summary>
    [HttpGet("api/analytics/processes/{processId:guid}/funnel")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessFunnelStepDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProcessFunnelStepDto>>> GetFunnel(
        Guid processId,
        [FromQuery] Guid? versionId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetProcessFunnelAsync(processId, versionId, from, to, ct));

    /// <summary>Сравнивает KPI двух версий процесса.</summary>
    [HttpGet("api/analytics/processes/{processId:guid}/version-comparison")]
    [ProducesResponseType(typeof(ProcessVersionComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProcessVersionComparisonDto>> GetVersionComparison(
        Guid processId,
        [FromQuery] Guid versionAId,
        [FromQuery] Guid versionBId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetVersionComparisonAsync(processId, versionAId, versionBId, from, to, ct));

    /// <summary>Возвращает сводный отчёт по всем процессам (Admin).</summary>
    [HttpGet("api/analytics/summary")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessAnalyticsSummaryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProcessAnalyticsSummaryItemDto>>> GetSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
        => Ok(await _service.GetAnalyticsSummaryAsync(from, to, ct));
}
