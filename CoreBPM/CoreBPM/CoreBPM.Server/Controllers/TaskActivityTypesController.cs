using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>Управление справочником видов деятельности для трудозатрат (FR-TASK-01.4).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class TaskActivityTypesController : ControllerBase
{
    private readonly AppDbContext _db;
    public TaskActivityTypesController(AppDbContext db) => _db = db;

    /// <summary>Получить список всех видов деятельности.</summary>
    [HttpGet("api/admin/activity-types")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskActivityTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskActivityTypeDto>>> List(CancellationToken ct)
    {
        var list = await _db.TaskActivityTypes.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new TaskActivityTypeDto { Id = a.Id, Name = a.Name, IsActive = a.IsActive, CreatedAt = a.CreatedAt })
            .ToListAsync(ct);
        return Ok(list);
    }

    /// <summary>Создать вид деятельности.</summary>
    [HttpPost("api/admin/activity-types")]
    [ProducesResponseType(typeof(TaskActivityTypeDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskActivityTypeDto>> Create([FromBody] UpsertActivityTypeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Название обязательно." });

        var now = DateTimeOffset.UtcNow;
        var entity = new TaskActivityType
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            IsActive = req.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.TaskActivityTypes.Add(entity);
        await _db.SaveChangesAsync(ct);

        var dto = new TaskActivityTypeDto { Id = entity.Id, Name = entity.Name, IsActive = entity.IsActive, CreatedAt = entity.CreatedAt };
        return CreatedAtAction(nameof(List), new { }, dto);
    }

    /// <summary>Обновить вид деятельности.</summary>
    [HttpPut("api/admin/activity-types/{id:guid}")]
    [ProducesResponseType(typeof(TaskActivityTypeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskActivityTypeDto>> Update(Guid id, [FromBody] UpsertActivityTypeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Название обязательно." });

        var entity = await _db.TaskActivityTypes.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Вид деятельности {id} не найден.");

        entity.Name = req.Name.Trim();
        entity.IsActive = req.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new TaskActivityTypeDto { Id = entity.Id, Name = entity.Name, IsActive = entity.IsActive, CreatedAt = entity.CreatedAt });
    }

    /// <summary>Удалить вид деятельности.</summary>
    [HttpDelete("api/admin/activity-types/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await _db.TaskActivityTypes.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Вид деятельности {id} не найден.");
        _db.TaskActivityTypes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
