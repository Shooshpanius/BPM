using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Настройки уведомлений пользователя по событиям задач (FR-TASK-02.3).</summary>
[ApiController]
[Authorize]
public class TaskNotificationSettingsController : ControllerBase
{
    private readonly ITaskService _service;

    public TaskNotificationSettingsController(ITaskService service)
        => _service = service;

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Получить текущие настройки уведомлений по задачам. FR-TASK-02.3.</summary>
    [HttpGet("api/users/me/notification-settings")]
    [ProducesResponseType(typeof(IReadOnlyList<UserTaskNotificationSettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserTaskNotificationSettingsDto>>> Get(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetNotificationSettingsAsync(userId.Value, ct));
    }

    /// <summary>Обновить настройки уведомлений по задачам. FR-TASK-02.3.</summary>
    [HttpPut("api/users/me/notification-settings")]
    [ProducesResponseType(typeof(IReadOnlyList<UserTaskNotificationSettingsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserTaskNotificationSettingsDto>>> Update(
        [FromBody] List<UpdateNotificationSettingRequest> settings, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateNotificationSettingsAsync(userId.Value, settings, ct));
    }
}
