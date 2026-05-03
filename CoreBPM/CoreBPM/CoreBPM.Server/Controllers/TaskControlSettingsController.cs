using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreBPM.Server.Controllers;

/// <summary>Системные настройки контроля и трудозатрат по задачам (FR-TASK-01.4).</summary>
[ApiController]
[Authorize]
public class TaskControlSettingsController : ControllerBase
{
    private readonly ITaskControlSettingsService _svc;

    public TaskControlSettingsController(ITaskControlSettingsService svc) => _svc = svc;

    /// <summary>Получить текущие настройки контроля и трудозатрат.</summary>
    [HttpGet("api/admin/task-control-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<TaskControlSettingsDto> Get(CancellationToken ct) =>
        await _svc.GetAsync(ct);

    /// <summary>Обновить настройки контроля и трудозатрат.</summary>
    [HttpPut("api/admin/task-control-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<TaskControlSettingsDto> Update(
        [FromBody] UpdateTaskControlSettingsRequest req,
        CancellationToken ct) =>
        await _svc.UpdateAsync(req, ct);
}
