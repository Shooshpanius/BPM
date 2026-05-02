using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API шаблонов задач (FR-TASK-01.1).</summary>
[ApiController]
[Authorize]
public class TaskTemplatesController : ControllerBase
{
    private readonly ITaskService _service;
    public TaskTemplatesController(ITaskService service) => _service = service;

    [HttpGet("api/task-templates")]
    public async Task<ActionResult<IReadOnlyList<TaskTemplateDto>>> List(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ListTemplatesAsync(userId.Value, ct));
    }

    [HttpPost("api/task-templates")]
    public async Task<ActionResult<TaskTemplateDto>> Create([FromBody] CreateTaskTemplateRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.CreateTemplateAsync(req, userId.Value, ct);
        return Created($"/api/task-templates/{dto.Id}", dto);
    }

    [HttpPut("api/task-templates/{id:guid}")]
    public async Task<ActionResult<TaskTemplateDto>> Update(Guid id, [FromBody] CreateTaskTemplateRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateTemplateAsync(id, req, userId.Value, ct));
    }

    [HttpDelete("api/task-templates/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.DeleteTemplateAsync(id, userId.Value, ct);
        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
