using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;
using CoreBPM.Server.Infrastructure.Workers;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления заданиями планировщика таймерных стартовых событий BPMN.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class BpmSchedulerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBpmExecutionEngine _engine;

    public BpmSchedulerController(AppDbContext db, IBpmExecutionEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    /// <summary>Возвращает список заданий планировщика с возможностью фильтрации.</summary>
    [HttpGet("api/bpm/scheduler-jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmSchedulerJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmSchedulerJobDto>>> GetJobs(
        [FromQuery] Guid? processId,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var query = _db.BpmSchedulerJobs.AsNoTracking().AsQueryable();

        if (processId.HasValue)
            query = query.Where(j => j.ProcessId == processId.Value);

        if (isActive.HasValue)
            query = query.Where(j => j.IsActive == isActive.Value);

        var jobs = await query
            .OrderBy(j => j.ProcessId)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync(ct);

        return Ok(jobs.Select(MapToDto).ToList());
    }

    /// <summary>Переключает активность задания планировщика (включить/выключить).</summary>
    [HttpPut("api/bpm/scheduler-jobs/{id:guid}")]
    [ProducesResponseType(typeof(BpmSchedulerJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmSchedulerJobDto>> ToggleActive(
        Guid id,
        [FromBody] ToggleSchedulerJobRequest request,
        CancellationToken ct)
    {
        var job = await _db.BpmSchedulerJobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job == null) return NotFound(new { error = "Задание планировщика не найдено" });

        var now = DateTimeOffset.UtcNow;
        job.IsActive = request.IsActive;
        job.UpdatedAt = now;

        // При активации вычисляем NextFireAt если он не задан
        if (job.IsActive && job.NextFireAt == null)
        {
            job.NextFireAt = ComputeInitialNextFireAt(job.TimerType, job.TimerValue, now);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(MapToDto(job));
    }

    /// <summary>Принудительно запускает задание планировщика — создаёт экземпляр процесса немедленно.</summary>
    [HttpPost("api/bpm/scheduler-jobs/{id:guid}/fire")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FireJob(Guid id, CancellationToken ct)
    {
        var job = await _db.BpmSchedulerJobs
            .Include(j => j.Process)
            .FirstOrDefaultAsync(j => j.Id == id, ct);

        if (job == null) return NotFound(new { error = "Задание планировщика не найдено" });

        var now = DateTimeOffset.UtcNow;
        var processName = job.Process?.Name ?? job.ProcessId.ToString();

        var instance = new BpmInstance
        {
            Id = Guid.NewGuid(),
            ProcessId = job.ProcessId,
            ProcessVersionId = job.ProcessVersionId,
            Name = $"{processName} — {now:dd.MM.yyyy HH:mm} (ручной запуск)",
            State = BpmInstanceState.Active,
            LaunchSource = BpmInstanceLaunchSource.Scheduler,
            StartedAt = now,
            UpdatedAt = now,
        };
        _db.BpmInstances.Add(instance);

        job.LastFiredAt = now;
        job.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        // Запускаем экземпляр асинхронно
        _ = _engine.StartAsync(instance.Id, CancellationToken.None);

        return NoContent();
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static BpmSchedulerJobDto MapToDto(BpmSchedulerJob j) => new(
        j.Id,
        j.ProcessId,
        j.ProcessVersionId,
        j.ElementId,
        j.TimerType,
        j.TimerValue,
        j.TimeZone,
        j.IsActive,
        j.LastFiredAt,
        j.NextFireAt,
        j.CreatedAt,
        j.UpdatedAt
    );

    /// <summary>
    /// Вычисляет начальное время срабатывания при ручной активации задания.
    /// </summary>
    private static DateTimeOffset? ComputeInitialNextFireAt(string timerType, string timerValue, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(timerValue)) return now;

        switch (timerType)
        {
            case "timeDate":
                if (DateTimeOffset.TryParse(timerValue, out var date))
                    return date;
                return now;

            case "timeDuration":
            case "timeCycle":
                var next = BpmSchedulerWorker.ComputeNextFireAt(timerValue, now);
                return next ?? now;

            default:
                return now;
        }
    }
}

/// <summary>Запрос на переключение активности задания планировщика.</summary>
public record ToggleSchedulerJobRequest(bool IsActive);
