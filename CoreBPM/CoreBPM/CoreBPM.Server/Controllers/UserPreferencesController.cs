using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер настроек пользователя (FR-ORG-02.3).</summary>
[ApiController]
[Route("api/users/{userId:guid}/preferences")]
[Authorize]
public class UserPreferencesController : ControllerBase
{
    private readonly IUserPreferencesService _service;

    public UserPreferencesController(IUserPreferencesService service)
    {
        _service = service;
    }

    /// <summary>Возвращает настройки пользователя.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserPreferencesDto>> GetPreferences(Guid userId, CancellationToken ct)
    {
        EnsureAccess(userId);
        return Ok(await _service.GetAsync(userId, ct));
    }

    /// <summary>Обновляет настройки пользователя.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(UserPreferencesDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserPreferencesDto>> UpdatePreferences(
        Guid userId, [FromBody] UpdatePreferencesRequest req, CancellationToken ct)
    {
        EnsureAccess(userId);
        return Ok(await _service.UpdateAsync(userId, req, ct));
    }

    // ── Вспомогательные методы ───────────────────────────────────────────────

    private void EnsureAccess(Guid userId)
    {
        var actorId = GetCurrentUserId();
        if (actorId != userId && !User.IsInRole("Admin"))
            throw new UnauthorizedAccessException("Доступ к настройкам другого пользователя запрещён.");
    }

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя.");
        return Guid.Parse(raw);
    }
}
