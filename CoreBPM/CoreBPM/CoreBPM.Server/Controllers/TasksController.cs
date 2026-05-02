using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления задачами (FR-TASK-01.1).</summary>
[ApiController]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _service;
    public TasksController(ITaskService service) => _service = service;

    /// <summary>Возвращает список задач с фильтрацией.</summary>
    [HttpGet("api/tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskSummaryDto>>> List([FromQuery] TaskListFilter filter, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ListAsync(userId.Value, User.IsInRole("Admin"), filter, ct));
    }

    /// <summary>Создаёт новую задачу.</summary>
    [HttpPost("api/tasks")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create([FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.CreateAsync(req, userId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Экспортирует задачи в CSV.</summary>
    [HttpGet("api/tasks/export")]
    public async Task<IActionResult> Export([FromQuery] TaskListFilter filter, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var bytes = await _service.ExportToCsvAsync(userId.Value, User.IsInRole("Admin"), filter, ct);
        return File(bytes, "text/csv;charset=utf-8", "tasks.csv");
    }

    /// <summary>Возвращает сохранённые фильтры.</summary>
    [HttpGet("api/tasks/saved-filters")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskSavedFilterDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskSavedFilterDto>>> GetSavedFilters(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetSavedFiltersAsync(userId.Value, ct));
    }

    /// <summary>Создаёт сохранённый фильтр.</summary>
    [HttpPost("api/tasks/saved-filters")]
    [ProducesResponseType(typeof(TaskSavedFilterDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskSavedFilterDto>> CreateSavedFilter([FromBody] CreateTaskSavedFilterRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.CreateSavedFilterAsync(userId.Value, req, ct);
        return Created($"/api/tasks/saved-filters/{dto.Id}", dto);
    }

    /// <summary>Удаляет сохранённый фильтр.</summary>
    [HttpDelete("api/tasks/saved-filters/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSavedFilter(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.DeleteSavedFilterAsync(id, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Возвращает задачу по идентификатору.</summary>
    [HttpGet("api/tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    /// <summary>Обновляет задачу.</summary>
    [HttpPut("api/tasks/{id:guid}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Update(Guid id, [FromBody] UpdateTaskRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateAsync(id, req, userId.Value, ct));
    }

    /// <summary>Удаляет задачу.</summary>
    [HttpDelete("api/tasks/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.DeleteAsync(id, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Копирует задачу.</summary>
    [HttpPost("api/tasks/{id:guid}/copy")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Copy(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.CopyAsync(id, userId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Помечает задачу как прочитанную.</summary>
    [HttpPost("api/tasks/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.MarkReadAsync(id, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Переназначает исполнителя.</summary>
    [HttpPut("api/tasks/{id:guid}/assignee")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Reassign(Guid id, [FromBody] ReassignTaskRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ReassignAsync(id, req, userId.Value, ct));
    }

    /// <summary>Создаёт подзадачу.</summary>
    [HttpPost("api/tasks/{id:guid}/subtasks")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> CreateSubtask(Guid id, [FromBody] CreateTaskRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.CreateSubtaskAsync(id, req, userId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>Возвращает комментарии задачи.</summary>
    [HttpGet("api/tasks/{id:guid}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskCommentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskCommentDto>>> GetComments(Guid id, CancellationToken ct)
        => Ok(await _service.GetCommentsAsync(id, ct));

    /// <summary>Добавляет комментарий.</summary>
    [HttpPost("api/tasks/{id:guid}/comments")]
    [ProducesResponseType(typeof(TaskCommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskCommentDto>> AddComment(Guid id, [FromBody] AddTaskCommentRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.AddCommentAsync(id, req, userId.Value, ct);
        return Created($"/api/tasks/{id}/comments/{dto.Id}", dto);
    }

    /// <summary>Возвращает вложения задачи.</summary>
    [HttpGet("api/tasks/{id:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskAttachmentDto>>> GetAttachments(Guid id, CancellationToken ct)
        => Ok(await _service.GetAttachmentsAsync(id, ct));

    /// <summary>Добавляет метаданные вложения.</summary>
    [HttpPost("api/tasks/{id:guid}/attachments")]
    [ProducesResponseType(typeof(TaskAttachmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskAttachmentDto>> AddAttachment(Guid id, [FromBody] AddTaskAttachmentRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.AddAttachmentAsync(id, req, userId.Value, ct);
        return Created($"/api/tasks/{id}/attachments/{dto.Id}", dto);
    }

    /// <summary>Возвращает участников задачи.</summary>
    [HttpGet("api/tasks/{id:guid}/participants")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskParticipantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskParticipantDto>>> GetParticipants(Guid id, CancellationToken ct)
        => Ok(await _service.GetParticipantsAsync(id, ct));

    /// <summary>Добавляет участника задачи.</summary>
    [HttpPost("api/tasks/{id:guid}/participants")]
    [ProducesResponseType(typeof(TaskParticipantDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskParticipantDto>> AddParticipant(Guid id, [FromBody] AddTaskParticipantRequest req, CancellationToken ct)
    {
        var dto = await _service.AddParticipantAsync(id, req, ct);
        return Created($"/api/tasks/{id}/participants/{dto.Id}", dto);
    }

    /// <summary>Удаляет участника задачи.</summary>
    [HttpDelete("api/tasks/{id:guid}/participants/{participantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId, CancellationToken ct)
    {
        await _service.RemoveParticipantAsync(id, participantId, ct);
        return NoContent();
    }

    /// <summary>Возвращает связи задачи.</summary>
    [HttpGet("api/tasks/{id:guid}/relations")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskRelationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskRelationDto>>> GetRelations(Guid id, CancellationToken ct)
        => Ok(await _service.GetRelationsAsync(id, ct));

    /// <summary>Добавляет связь задачи.</summary>
    [HttpPost("api/tasks/{id:guid}/relations")]
    [ProducesResponseType(typeof(TaskRelationDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskRelationDto>> AddRelation(Guid id, [FromBody] AddTaskRelationRequest req, CancellationToken ct)
    {
        var dto = await _service.AddRelationAsync(id, req, ct);
        return Created($"/api/tasks/{id}/relations/{dto.Id}", dto);
    }

    /// <summary>Удаляет связь задачи.</summary>
    [HttpDelete("api/tasks/{id:guid}/relations/{relationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveRelation(Guid id, Guid relationId, CancellationToken ct)
    {
        await _service.RemoveRelationAsync(id, relationId, ct);
        return NoContent();
    }

    /// <summary>Добавляет тег задачи.</summary>
    [HttpPost("api/tasks/{id:guid}/tags")]
    [ProducesResponseType(typeof(TaskTagResultDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskTagResultDto>> AddTag(Guid id, [FromBody] AddTaskTagRequest req, CancellationToken ct)
    {
        var dto = await _service.AddTagAsync(id, req, ct);
        return Created($"/api/tasks/{id}/tags/{dto.Id}", dto);
    }

    /// <summary>Удаляет тег задачи.</summary>
    [HttpDelete("api/tasks/{id:guid}/tags/{tagId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTag(Guid id, Guid tagId, CancellationToken ct)
    {
        await _service.RemoveTagAsync(id, tagId, ct);
        return NoContent();
    }

    /// <summary>Возвращает историю изменений задачи.</summary>
    [HttpGet("api/tasks/{id:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskHistoryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskHistoryEntryDto>>> GetHistory(Guid id, CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(id, ct));

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
