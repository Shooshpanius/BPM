using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API сохранённых фильтров раздела «Мои процессы».</summary>
[ApiController]
[Authorize]
public class BpmSavedFiltersController : ControllerBase
{
    private readonly IBpmSavedFilterService _service;

    public BpmSavedFiltersController(IBpmSavedFilterService service)
    {
        _service = service;
    }

    /// <summary>Возвращает сохранённые фильтры текущего пользователя.</summary>
    [HttpGet("api/bpm/saved-filters")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmSavedFilterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmSavedFilterDto>>> GetAll(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetFiltersAsync(userId.Value, ct));
    }

    /// <summary>Создаёт новый сохранённый фильтр.</summary>
    [HttpPost("api/bpm/saved-filters")]
    [ProducesResponseType(typeof(BpmSavedFilterDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BpmSavedFilterDto>> Create(
        [FromBody] SaveFilterRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.CreateFilterAsync(userId.Value, request, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    /// <summary>Обновляет сохранённый фильтр.</summary>
    [HttpPut("api/bpm/saved-filters/{filterId}")]
    [ProducesResponseType(typeof(BpmSavedFilterDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BpmSavedFilterDto>> Update(
        Guid filterId,
        [FromBody] SaveFilterRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateFilterAsync(filterId, userId.Value, request, ct));
    }

    /// <summary>Удаляет сохранённый фильтр.</summary>
    [HttpDelete("api/bpm/saved-filters/{filterId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid filterId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.DeleteFilterAsync(filterId, userId.Value, ct);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
