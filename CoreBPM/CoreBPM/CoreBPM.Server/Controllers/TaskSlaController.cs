using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления SLA-правилами задач (FR-TASK-01.2). Только для Admin.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class TaskSlaController : ControllerBase
{
    private readonly ITaskSlaService _service;

    public TaskSlaController(ITaskSlaService service) => _service = service;

    /// <summary>Возвращает список SLA-правил.</summary>
    [HttpGet("api/admin/task-sla")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskSlaRuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskSlaRuleDto>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    /// <summary>Создаёт SLA-правило.</summary>
    [HttpPost("api/admin/task-sla")]
    [ProducesResponseType(typeof(TaskSlaRuleDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskSlaRuleDto>> Create([FromBody] UpsertTaskSlaRuleRequest req, CancellationToken ct)
    {
        var dto = await _service.CreateAsync(req, ct);
        return Created($"/api/admin/task-sla/{dto.Id}", dto);
    }

    /// <summary>Обновляет SLA-правило.</summary>
    [HttpPut("api/admin/task-sla/{id:guid}")]
    [ProducesResponseType(typeof(TaskSlaRuleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskSlaRuleDto>> Update(Guid id, [FromBody] UpsertTaskSlaRuleRequest req, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, req, ct));

    /// <summary>Удаляет SLA-правило.</summary>
    [HttpDelete("api/admin/task-sla/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
