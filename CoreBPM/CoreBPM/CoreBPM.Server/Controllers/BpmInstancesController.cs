using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления экземплярами бизнес-процессов.</summary>
[ApiController]
[Authorize]
public class BpmInstancesController : ControllerBase
{
    private readonly IBpmInstanceService _service;

    public BpmInstancesController(IBpmInstanceService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список экземпляров процесса.</summary>
    [HttpGet("api/bpm/processes/{processId}/instances")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmInstanceListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmInstanceListItemDto>>> GetByProcess(
        Guid processId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _service.GetInstancesAsync(processId, page, pageSize, ct));

    /// <summary>Запускает новый экземпляр процесса вручную.</summary>
    [HttpPost("api/bpm/processes/{processId}/instances")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> Create(
        Guid processId,
        [FromBody] CreateInstanceRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var instance = await _service.CreateInstanceAsync(processId, request, userId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { instanceId = instance.Id }, instance);
    }

    /// <summary>Возвращает экземпляр процесса по идентификатору.</summary>
    [HttpGet("api/bpm/instances/{instanceId}")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> GetById(
        Guid instanceId,
        CancellationToken ct)
        => Ok(await _service.GetInstanceByIdAsync(instanceId, ct));

    /// <summary>Возвращает задания планировщика для процесса (таймерные стартовые события).</summary>
    [HttpGet("api/bpm/processes/{processId}/scheduler-jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmSchedulerJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmSchedulerJobDto>>> GetSchedulerJobs(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetSchedulerJobsAsync(processId, ct));

    // ─── Управление состоянием ────────────────────────────────────────────────

    /// <summary>Прерывает (отменяет) экземпляр с указанием причины.</summary>
    [HttpPost("api/bpm/instances/{instanceId}/cancel")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> Cancel(
        Guid instanceId,
        [FromBody] CancelInstanceRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.CancelInstanceAsync(instanceId, request, userId.Value, ct));
    }

    /// <summary>Приостанавливает выполнение экземпляра.</summary>
    [HttpPost("api/bpm/instances/{instanceId}/suspend")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BpmInstanceDto>> Suspend(Guid instanceId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.SuspendInstanceAsync(instanceId, userId.Value, ct));
    }

    /// <summary>Возобновляет приостановленный экземпляр.</summary>
    [HttpPost("api/bpm/instances/{instanceId}/resume")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BpmInstanceDto>> Resume(Guid instanceId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ResumeInstanceAsync(instanceId, userId.Value, ct));
    }

    /// <summary>Изменяет ответственного за экземпляр.</summary>
    [HttpPut("api/bpm/instances/{instanceId}/responsible")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BpmInstanceDto>> ChangeResponsible(
        Guid instanceId,
        [FromBody] ChangeResponsibleRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.ChangeResponsibleAsync(instanceId, request, userId.Value, ct));
    }

    /// <summary>Обновляет значение переменной экземпляра.</summary>
    [HttpPut("api/bpm/instances/{instanceId}/variables/{variableName}")]
    [ProducesResponseType(typeof(BpmInstanceVariableDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BpmInstanceVariableDto>> UpdateVariable(
        Guid instanceId,
        string variableName,
        [FromBody] UpdateInstanceVariableRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.UpdateVariableAsync(instanceId, variableName, request, userId.Value, ct));
    }

    // ─── История ─────────────────────────────────────────────────────────────

    /// <summary>Возвращает журнал событий экземпляра.</summary>
    [HttpGet("api/bpm/instances/{instanceId}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmInstanceHistoryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmInstanceHistoryEntryDto>>> GetHistory(
        Guid instanceId,
        CancellationToken ct)
        => Ok(await _service.GetHistoryAsync(instanceId, ct));

    /// <summary>Добавляет комментарий или вопрос к экземпляру.</summary>
    [HttpPost("api/bpm/instances/{instanceId}/comments")]
    [ProducesResponseType(typeof(BpmInstanceHistoryEntryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BpmInstanceHistoryEntryDto>> AddComment(
        Guid instanceId,
        [FromBody] AddCommentRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var entry = await _service.AddCommentAsync(instanceId, request, userId.Value, ct);
        return StatusCode(StatusCodes.Status201Created, entry);
    }

    // ─── Участники ───────────────────────────────────────────────────────────

    /// <summary>Возвращает список участников экземпляра.</summary>
    [HttpGet("api/bpm/instances/{instanceId}/participants")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmInstanceParticipantDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmInstanceParticipantDto>>> GetParticipants(
        Guid instanceId,
        CancellationToken ct)
        => Ok(await _service.GetParticipantsAsync(instanceId, ct));

    /// <summary>Добавляет участника к экземпляру.</summary>
    [HttpPost("api/bpm/instances/{instanceId}/participants")]
    [ProducesResponseType(typeof(BpmInstanceParticipantDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<BpmInstanceParticipantDto>> AddParticipant(
        Guid instanceId,
        [FromBody] AddParticipantRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var p = await _service.AddParticipantAsync(instanceId, request, userId.Value, ct);
        return StatusCode(StatusCodes.Status201Created, p);
    }

    /// <summary>Удаляет участника из экземпляра.</summary>
    [HttpDelete("api/bpm/instances/{instanceId}/participants/{participantUserId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveParticipant(
        Guid instanceId,
        Guid participantUserId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        await _service.RemoveParticipantAsync(instanceId, participantUserId, userId.Value, ct);
        return NoContent();
    }

    // ─── Мои процессы ────────────────────────────────────────────────────────

    /// <summary>Возвращает экземпляры, в которых текущий пользователь является инициатором, ответственным или участником.</summary>
    [HttpGet("api/bpm/instances/my")]
    [ProducesResponseType(typeof(MyInstancesResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<MyInstancesResult>> GetMy(
        [FromQuery] MyInstancesRole role = MyInstancesRole.All,
        [FromQuery] BpmInstanceState? state = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? processId = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var filter = new MyInstancesFilter(role, state, search, processId, dateFrom, dateTo);
        return Ok(await _service.GetMyInstancesAsync(userId.Value, filter, page, pageSize, ct));
    }

    /// <summary>Экспортирует результаты поиска «Мои процессы» в CSV-файл.</summary>
    [HttpGet("api/bpm/instances/my/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMy(
        [FromQuery] MyInstancesRole role = MyInstancesRole.All,
        [FromQuery] BpmInstanceState? state = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? processId = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var filter = new MyInstancesFilter(role, state, search, processId, dateFrom, dateTo);
        var bytes = await _service.ExportMyInstancesToCsvAsync(userId.Value, filter, ct);
        return File(bytes, "text/csv; charset=utf-8", "my-processes.csv");
    }

    // ─── Пакетный запуск ─────────────────────────────────────────────────────

    /// <summary>Пакетный запуск нескольких экземпляров одного процесса.</summary>
    [HttpPost("api/bpm/processes/{processId}/instances/batch")]
    [ProducesResponseType(typeof(BatchLaunchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchLaunchResult>> BatchCreate(
        Guid processId,
        [FromBody] BatchLaunchRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.BatchCreateInstancesAsync(processId, request, userId.Value, ct));
    }

    // ─── Переключение версии ─────────────────────────────────────────────────

    /// <summary>Переключает работающий экземпляр на другую опубликованную версию того же процесса.</summary>
    [HttpPut("api/bpm/instances/{instanceId}/version")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> SwitchVersion(
        Guid instanceId,
        [FromBody] SwitchInstanceVersionRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        return Ok(await _service.SwitchVersionAsync(instanceId, request, userId.Value, ct));
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
