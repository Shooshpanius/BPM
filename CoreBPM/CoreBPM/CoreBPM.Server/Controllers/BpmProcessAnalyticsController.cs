using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>API аналитики и KPI бизнес-процессов (FR-BPM-03.2).</summary>
[ApiController]
[Authorize]
public class BpmProcessAnalyticsController : ControllerBase
{
    private readonly IBpmAnalyticsService _service;
    private readonly AppDbContext _db;

    public BpmProcessAnalyticsController(IBpmAnalyticsService service, AppDbContext db)
    {
        _service = service;
        _db = db;
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

    /// <summary>Экспортирует сводный отчёт по всем процессам в файл Excel (Admin).</summary>
    [HttpGet("api/analytics/summary/export")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportSummary(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var bytes = await _service.ExportSummaryToExcelAsync(from, to, ct);
        var fileName = $"analytics_summary_{DateTimeOffset.UtcNow:yyyyMMdd_HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    /// <summary>Возвращает тренд KPI процесса по периодам (FR-BPM-03.2).</summary>
    [HttpGet("api/analytics/processes/{processId:guid}/trend")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessTrendPointDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProcessTrendPointDto>>> GetTrend(
        Guid processId,
        [FromQuery] string granularity = "week",
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
        => Ok(await _service.GetProcessTrendAsync(processId, granularity, from, to, ct));

    /// <summary>Возвращает список последних KPI-алертов (Admin).</summary>
    [HttpGet("api/admin/kpi-alerts")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<KpiAlertDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KpiAlertDto>>> GetKpiAlerts(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var alerts = await _db.BpmKpiAlerts
            .AsNoTracking()
            .OrderByDescending(a => a.DetectedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(a => new KpiAlertDto(
                a.Id,
                a.ProcessId,
                a.ProcessName,
                a.AvgCycleTimeMinutes,
                a.TargetCycleTimeMinutes,
                a.ExceedPercent,
                a.DetectedAt))
            .ToListAsync(ct);

        return Ok(alerts);
    }
}
