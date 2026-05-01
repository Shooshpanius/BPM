using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API документирования процессов (FR-BPM-02.6).</summary>
[ApiController]
[Authorize]
public class BpmDocumentationController : ControllerBase
{
    private readonly IBpmDocumentationService _service;

    public BpmDocumentationController(IBpmDocumentationService service)
    {
        _service = service;
    }

    /// <summary>
    /// Возвращает список процессов текущего пользователя (Владелец/Куратор)
    /// с таблицей опубликованных версий.
    /// </summary>
    [HttpGet("api/bpm/documentation/my")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessDocumentationItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ProcessDocumentationItemDto>>> GetMy(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(await _service.GetMyDocumentationAsync(userId.Value, ct));
    }

    /// <summary>
    /// Возвращает полный список процессов системы с таблицей опубликованных версий.
    /// Доступно только администраторам.
    /// </summary>
    [HttpGet("api/bpm/documentation/all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ProcessDocumentationItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProcessDocumentationItemDto>>> GetAll(
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
        => Ok(await _service.GetAllDocumentationAsync(includeDeleted, ct));

    /// <summary>
    /// Возвращает HTML-снапшот документации указанной версии процесса.
    /// </summary>
    [HttpGet("api/bpm/processes/{processId:guid}/versions/{versionId:guid}/snapshot")]
    [ProducesResponseType(typeof(DocSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocSnapshotDto>> GetSnapshot(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
        => Ok(await _service.GetDocSnapshotAsync(processId, versionId, ct));

    /// <summary>
    /// Генерирует (пересоздаёт) HTML-снапшот документации для указанной версии.
    /// Доступно только администраторам.
    /// </summary>
    [HttpPost("api/bpm/processes/{processId:guid}/versions/{versionId:guid}/snapshot/regenerate")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateSnapshot(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId() ?? Guid.Empty;
        await _service.GenerateAndSaveSnapshotAsync(processId, versionId, userId, ct);
        return NoContent();
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
