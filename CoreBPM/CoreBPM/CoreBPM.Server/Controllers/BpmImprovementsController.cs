using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Controllers;

/// <summary>API предложений по улучшению бизнес-процессов (FR-BPM-03.1).</summary>
[ApiController]
[Authorize]
public class BpmImprovementsController : ControllerBase
{
    private readonly IBpmImprovementService _service;

    public BpmImprovementsController(IBpmImprovementService service)
    {
        _service = service;
    }

    /// <summary>Создаёт предложение по улучшению указанного процесса.</summary>
    [HttpPost("api/bpm/processes/{processId}/improvements")]
    [ProducesResponseType(typeof(ImprovementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImprovementDto>> Create(
        Guid processId,
        [FromBody] CreateImprovementRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var dto = await _service.CreateAsync(processId, request, userId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Возвращает список предложений по указанному процессу.</summary>
    [HttpGet("api/bpm/processes/{processId}/improvements")]
    [ProducesResponseType(typeof(IReadOnlyList<ImprovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ImprovementDto>>> ListByProcess(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.ListByProcessAsync(processId, ct));

    /// <summary>Возвращает общий список предложений с фильтрацией.</summary>
    [HttpGet("api/bpm/improvements")]
    [ProducesResponseType(typeof(IReadOnlyList<ImprovementDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ImprovementDto>>> List(
        [FromQuery] string role = "All",
        [FromQuery] Guid? processId = null,
        [FromQuery] BpmImprovementStatus? status = null,
        [FromQuery] Guid? authorId = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var isAdmin = User.IsInRole("Admin");

        var result = await _service.ListAsync(userId.Value, isAdmin, role, processId, status, authorId, dateFrom, dateTo, ct);
        return Ok(result);
    }

    /// <summary>Возвращает предложение по идентификатору.</summary>
    [HttpGet("api/bpm/improvements/{id}")]
    [ProducesResponseType(typeof(ImprovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImprovementDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    /// <summary>Принимает предложение (владелец процесса).</summary>
    [HttpPost("api/bpm/improvements/{id}/accept")]
    [ProducesResponseType(typeof(ImprovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImprovementDto>> Accept(
        Guid id,
        [FromBody] AcceptImprovementRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.AcceptAsync(id, request, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Отклоняет предложение (владелец процесса).</summary>
    [HttpPost("api/bpm/improvements/{id}/reject")]
    [ProducesResponseType(typeof(ImprovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImprovementDto>> Reject(
        Guid id,
        [FromBody] RejectImprovementRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.RejectAsync(id, request, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Завершает реализацию улучшения (исполнитель или Admin).</summary>
    [HttpPost("api/bpm/improvements/{id}/complete")]
    [ProducesResponseType(typeof(ImprovementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImprovementDto>> Complete(
        Guid id,
        [FromBody] CompleteImprovementRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.CompleteAsync(id, request, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Возвращает монитор улучшений для процессов текущего пользователя (Владелец/Куратор).</summary>
    [HttpGet("api/bpm/improvements/monitor/my")]
    [ProducesResponseType(typeof(IReadOnlyList<ImprovementMonitorItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ImprovementMonitorItemDto>>> MonitorMy(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetMonitorMyAsync(userId.Value, ct));
    }

    /// <summary>Возвращает полный монитор улучшений по всем процессам. Только для администраторов.</summary>
    [HttpGet("api/bpm/improvements/monitor/full")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ImprovementMonitorItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ImprovementMonitorItemDto>>> MonitorFull(CancellationToken ct)
        => Ok(await _service.GetMonitorFullAsync(ct));

    /// <summary>Экспортирует список предложений по улучшению в CSV-файл (FR-BPM-03.1).</summary>
    [HttpGet("api/bpm/improvements/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var isAdmin = User.IsInRole("Admin");

        var bytes = await _service.ExportToCsvAsync(userId.Value, isAdmin, ct);
        var fileName = $"improvements_{DateTimeOffset.UtcNow:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
