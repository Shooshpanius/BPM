using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Дашборд задач пользователя (FR-TASK-02.3).</summary>
[ApiController]
[Authorize]
public class TaskDashboardController : ControllerBase
{
    private readonly ITaskService _service;
    private readonly ILogger<TaskDashboardController> _logger;

    public TaskDashboardController(ITaskService service, ILogger<TaskDashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Получить виджеты дашборда задач (открытые задачи по приоритетам, статусам, динамика за 30 дней). FR-TASK-02.3.</summary>
    [HttpGet("api/dashboard/tasks")]
    [ProducesResponseType(typeof(TaskDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetDashboardAsync(userId.Value, User.IsInRole("Admin"), ct));
    }
}
