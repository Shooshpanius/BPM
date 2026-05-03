using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления задачами (FR-TASK-01.1, FR-TASK-01.2).</summary>
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
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.AddParticipantAsync(id, req, userId.Value, ct);
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
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.AddRelationAsync(id, req, userId.Value, ct);
        return Created($"/api/tasks/{id}/relations/{dto.Id}", dto);
    }

    /// <summary>Удаляет связь задачи.</summary>
    [HttpDelete("api/tasks/{id:guid}/relations/{relationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveRelation(Guid id, Guid relationId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.RemoveRelationAsync(id, relationId, userId.Value, ct);
        return NoContent();
    }

    /// <summary>Добавляет тег задачи.</summary>
    [HttpPost("api/tasks/{id:guid}/tags")]
    [ProducesResponseType(typeof(TaskTagResultDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskTagResultDto>> AddTag(Guid id, [FromBody] AddTaskTagRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.AddTagAsync(id, req, userId.Value, ct);
        return Created($"/api/tasks/{id}/tags/{dto.Id}", dto);
    }

    /// <summary>Удаляет тег задачи.</summary>
    [HttpDelete("api/tasks/{id:guid}/tags/{tagId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveTag(Guid id, Guid tagId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.RemoveTagAsync(id, tagId, userId.Value, ct);
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

    // ─── FR-TASK-01.2: Действия по статусам ────────────────────────────────────

    /// <summary>Возвращает список допустимых действий для текущего пользователя над задачей.</summary>
    [HttpGet("api/tasks/{id:guid}/actions")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskAllowedActionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskAllowedActionDto>>> GetAllowedActions(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetAllowedActionsAsync(id, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Начать работу по задаче (New/Read → InProgress).</summary>
    [HttpPost("api/tasks/{id:guid}/actions/start")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> StartWork(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.StartWorkAsync(id, userId.Value, ct));
    }

    /// <summary>Отметить задачу как выполненную (InProgress → Done/DoneNeedsControl).</summary>
    [HttpPost("api/tasks/{id:guid}/actions/done")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> MarkDone(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.MarkDoneAsync(id, userId.Value, ct));
    }

    /// <summary>Отметить задачу как невозможную для выполнения (InProgress → CannotDo/CannotDoNeedsControl).</summary>
    [HttpPost("api/tasks/{id:guid}/actions/cannot-do")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> CannotDo(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.MarkCannotDoAsync(id, userId.Value, ct));
    }

    /// <summary>Закрыть (отменить) задачу (→ Closed). Только автор или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/close")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> CloseTask(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.CloseAsync(id, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Отложить задачу до указанной даты (→ Postponed).</summary>
    [HttpPost("api/tasks/{id:guid}/actions/postpone")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Postpone(Guid id, [FromBody] PostponeTaskRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.PostponeAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Принять контроль: DoneNeedsControl/CannotDoNeedsControl → *Controlled. Только контролёр или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/accept-control")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> AcceptControl(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.AcceptControlAsync(id, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Вернуть задачу на доработку: DoneNeedsControl/CannotDoNeedsControl → New. Только контролёр или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/return")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> ReturnToWork(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ReturnToWorkAsync(id, userId.Value, User.IsInRole("Admin"), ct));
    }

    // ─── FR-TASK-01.3: Согласование ──────────────────────────────────────────

    /// <summary>Согласовать предварительное согласование (PreApproval → New). Согласующий или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/approve-pre")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> ApprovePre(Guid id, [FromBody] ApprovalDecisionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ApprovePreAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Отказать в предварительном согласовании (PreApproval → PreApprovalRejected). Согласующий или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/reject-pre")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> RejectPre(Guid id, [FromBody] ApprovalDecisionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.RejectPreAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Отправить задачу на согласование (New/Read/InProgress → OnApproval). Исполнитель или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/send-for-approval")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> SendForApproval(Guid id, [FromBody] SendForApprovalRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.SendForApprovalAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Согласовать задачу (OnApproval → New). Согласующий или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/approve")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Approve(Guid id, [FromBody] ApprovalDecisionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ApproveAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Отказать в согласовании (OnApproval → ApprovalRejected). Согласующий или Admin.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/reject")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> Reject(Guid id, [FromBody] ApprovalDecisionRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.RejectAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Получить текущее состояние согласования задачи.</summary>
    [HttpGet("api/tasks/{id:guid}/approval")]
    [ProducesResponseType(typeof(TaskApprovalStateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskApprovalStateDto>> GetApprovalState(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetApprovalStateAsync(id, ct));
    }

    // ─── FR-TASK-01.4: Контроль и трудозатраты ───────────────────────────────

    /// <summary>Изменить контролёра и/или тип контроля задачи.</summary>
    [HttpPut("api/tasks/{id:guid}/control")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> UpdateControl(Guid id, [FromBody] UpdateControlRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateControlAsync(id, req, userId.Value, User.IsInRole("Admin"), ct));
    }

    /// <summary>Добавить трудозатраты к задаче.</summary>
    [HttpPost("api/tasks/{id:guid}/timelogs")]
    [ProducesResponseType(typeof(TaskTimeLogDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskTimeLogDto>> AddTimeLog(Guid id, [FromBody] AddTimeLogRequest req, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var dto = await _service.AddTimeLogAsync(id, req, userId.Value, ct);
        return Created($"/api/tasks/{id}/timelogs/{dto.Id}", dto);
    }

    /// <summary>Получить журнал трудозатрат по задаче.</summary>
    [HttpGet("api/tasks/{id:guid}/timelogs")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskTimeLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TaskTimeLogDto>>> GetTimeLogs(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.GetTimeLogsAsync(id, ct));
    }

    /// <summary>Удалить запись трудозатрат.</summary>
    [HttpDelete("api/tasks/{id:guid}/timelogs/{logId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTimeLog(Guid id, Guid logId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.DeleteTimeLogAsync(id, logId, userId.Value, User.IsInRole("Admin"), ct);
        return NoContent();
    }

    /// <summary>Взять задачу на текущий контроль.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/take-control")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> TakeControl(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.TakeControlAsync(id, userId.Value, ct));
    }

    /// <summary>Снять задачу с контроля.</summary>
    [HttpPost("api/tasks/{id:guid}/actions/release-control")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaskDto>> ReleaseControl(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ReleaseControlAsync(id, userId.Value, User.IsInRole("Admin"), ct));
    }
}

