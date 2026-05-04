using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер профиля пользователя (FR-ORG-02.1).</summary>
[ApiController]
[Route("api/users/{userId:guid}")]
[Authorize]
public class UserProfileController : ControllerBase
{
    private readonly IUserProfileService _service;

    public UserProfileController(IUserProfileService service)
    {
        _service = service;
    }

    /// <summary>Возвращает профиль пользователя.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetProfile(Guid userId, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var dto = await _service.GetProfileAsync(userId, actorId, isAdmin, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Обновляет профиль пользователя.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> UpdateProfile(
        Guid userId, [FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var dto = await _service.UpdateProfileAsync(userId, actorId, req, isAdmin, ct);
        return Ok(dto);
    }

    /// <summary>Устанавливает аватар пользователя (заглушка: возвращает плейсхолдер URL).</summary>
    [HttpPost("avatar")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadAvatar(Guid userId, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var url = await _service.SetAvatarUrlAsync(userId, actorId, isAdmin, ct);
        return Ok(new { avatarUrl = url });
    }

    /// <summary>Удаляет аватар пользователя.</summary>
    [HttpDelete("avatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAvatar(Guid userId, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        await _service.DeleteAvatarAsync(userId, actorId, isAdmin, ct);
        return NoContent();
    }

    // ── Вспомогательный метод ────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя.");
        return Guid.Parse(raw);
    }
}
