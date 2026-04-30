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
    private readonly IBpmDiagramLockService _lockService;

    public BpmDiagramController(IBpmProcessService service, IBpmDiagramLockService lockService)
    {
        _service = service;
        _lockService = lockService;
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

    // ─── Блокировки ───────────────────────────────────────────────────────────

    /// <summary>
    /// Возвращает информацию об активной блокировке диаграммы.
    /// Если диаграмма не заблокирована — 204 No Content.
    /// </summary>
    [HttpGet("lock")]
    [ProducesResponseType(typeof(DiagramLockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<DiagramLockDto>> GetLock(Guid processId, CancellationToken ct)
    {
        var @lock = await _lockService.GetLockAsync(processId, ct);
        return @lock is null ? NoContent() : Ok(@lock);
    }

    /// <summary>
    /// Захватывает (или продлевает) блокировку диаграммы.
    /// Возвращает IsAcquired=false если диаграмма уже заблокирована другим пользователем.
    /// </summary>
    [HttpPut("lock")]
    [ProducesResponseType(typeof(AcquireLockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcquireLockResponse>> AcquireLock(Guid processId, CancellationToken ct)
    {
        var userId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя");

        var displayName = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("name")
            ?? userId.ToString();

        var result = await _lockService.AcquireAsync(processId, userId, displayName, ct);
        return Ok(result);
    }

    /// <summary>
    /// Снимает блокировку, захваченную текущим пользователем.
    /// Возвращает 204 в любом случае (идемпотентно).
    /// </summary>
    [HttpDelete("lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseLock(Guid processId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId.HasValue)
            await _lockService.ReleaseAsync(processId, userId.Value, ct);

        return NoContent();
    }

    /// <summary>
    /// Принудительно снимает блокировку (только для администраторов).
    /// </summary>
    [HttpDelete("lock/force")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForceReleaseLock(Guid processId, CancellationToken ct)
    {
        await _lockService.ForceReleaseAsync(processId, ct);
        return NoContent();
    }

    // ─── Вспомогательные методы ───

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
