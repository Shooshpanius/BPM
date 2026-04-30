using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления бизнес-процессами (CRUD).</summary>
[ApiController]
[Route("api/bpm/processes")]
[Authorize]
public class BpmProcessesController : ControllerBase
{
    private readonly IBpmProcessService _service;

    public BpmProcessesController(IBpmProcessService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список процессов организации.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessListItemDto>>> GetAll(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.GetProcessesAsync(organizationId, ct));

    /// <summary>Возвращает процесс по идентификатору.</summary>
    [HttpGet("{processId:guid}")]
    [ProducesResponseType(typeof(BpmProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessDto>> GetById(Guid processId, CancellationToken ct)
        => Ok(await _service.GetProcessByIdAsync(processId, ct));

    /// <summary>Создаёт новый процесс.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BpmProcessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BpmProcessDto>> Create(
        [FromBody] CreateBpmProcessRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя");

        var result = await _service.CreateProcessAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { processId = result.Id }, result);
    }

    /// <summary>Обновляет метаданные процесса (название, описание).</summary>
    [HttpPut("{processId:guid}")]
    [ProducesResponseType(typeof(BpmProcessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessDto>> Update(
        Guid processId,
        [FromBody] UpdateBpmProcessRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateProcessAsync(processId, request, ct));

    /// <summary>Удаляет процесс (мягкое удаление).</summary>
    [HttpDelete("{processId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid processId, CancellationToken ct)
    {
        await _service.DeleteProcessAsync(processId, ct);
        return NoContent();
    }

    /// <summary>Возвращает список версий процесса.</summary>
    [HttpGet("{processId:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessVersionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessVersionInfoDto>>> GetVersions(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetVersionsAsync(processId, ct));

    /// <summary>Возвращает конкретную версию процесса.</summary>
    [HttpGet("{processId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType(typeof(BpmDiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDiagramDto>> GetVersion(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
        => Ok(await _service.GetVersionAsync(processId, versionId, ct));

    /// <summary>Публикует указанную версию процесса.</summary>
    [HttpPost("{processId:guid}/versions/{versionId:guid}/publish")]
    [ProducesResponseType(typeof(BpmProcessVersionInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessVersionInfoDto>> Publish(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
        => Ok(await _service.PublishVersionAsync(processId, versionId, ct));

    /// <summary>Создаёт новый черновик-копию из исторической версии.</summary>
    [HttpPost("{processId:guid}/versions/{versionId:guid}/rollback")]
    [ProducesResponseType(typeof(BpmDiagramDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDiagramDto>> Rollback(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId()
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя");

        var result = await _service.RollbackVersionAsync(processId, versionId, userId, ct);
        return CreatedAtAction(nameof(GetVersion), new { processId, versionId = result.VersionId }, result);
    }

    /// <summary>Выполняет валидацию процесса перед публикацией.</summary>
    [HttpPost("{processId:guid}/validate")]
    [ProducesResponseType(typeof(BpmValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmValidationResultDto>> Validate(
        Guid processId,
        [FromBody] ValidateBpmProcessRequest? request,
        CancellationToken ct)
        => Ok(await _service.ValidateProcessAsync(processId, request?.VersionId, ct));

    /// <summary>Сравнивает две версии процесса.</summary>
    [HttpPost("{processId:guid}/diff")]
    [ProducesResponseType(typeof(BpmVersionDiffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmVersionDiffDto>> Diff(
        Guid processId,
        [FromBody] BpmVersionDiffRequest request,
        CancellationToken ct)
        => Ok(await _service.DiffVersionsAsync(processId, request.LeftVersionId, request.RightVersionId, ct));

    /// <summary>Возвращает настройки процесса.</summary>
    [HttpGet("{processId:guid}/settings")]
    [ProducesResponseType(typeof(BpmProcessSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessSettingsDto>> GetSettings(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.GetSettingsAsync(processId, ct));

    /// <summary>Обновляет настройки процесса.</summary>
    [HttpPut("{processId:guid}/settings")]
    [ProducesResponseType(typeof(BpmProcessSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmProcessSettingsDto>> UpdateSettings(
        Guid processId,
        [FromBody] UpdateBpmProcessSettingsRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateSettingsAsync(processId, request, ct));

    /// <summary>Ротирует токен внешнего запуска процесса.</summary>
    [HttpPost("{processId:guid}/settings/external-token:rotate")]
    [ProducesResponseType(typeof(RotateExternalTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RotateExternalTokenResponse>> RotateExternalToken(
        Guid processId,
        CancellationToken ct)
        => Ok(await _service.RotateExternalTokenAsync(processId, ct));

    /// <summary>Запускает debug-сессию процесса.</summary>
    [HttpPost("{processId:guid}/debug")]
    [ProducesResponseType(typeof(BpmDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDebugSessionDto>> StartDebug(
        Guid processId,
        [FromBody] StartBpmDebugSessionRequest request,
        CancellationToken ct)
        => Ok(await _service.StartDebugSessionAsync(processId, request, ct));

    /// <summary>Возвращает состояние debug-сессии процесса.</summary>
    [HttpGet("{processId:guid}/debug/{sessionId:guid}")]
    [ProducesResponseType(typeof(BpmDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDebugSessionDto>> GetDebug(
        Guid processId,
        Guid sessionId,
        CancellationToken ct)
        => Ok(await _service.GetDebugSessionAsync(processId, sessionId, ct));

    /// <summary>Выполняет один шаг debug-сессии процесса.</summary>
    [HttpPost("{processId:guid}/debug/{sessionId:guid}/step")]
    [ProducesResponseType(typeof(BpmDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDebugSessionDto>> StepDebug(
        Guid processId,
        Guid sessionId,
        CancellationToken ct)
        => Ok(await _service.StepDebugSessionAsync(processId, sessionId, "step", ct));

    /// <summary>Завершает текущую пользовательскую задачу в debug-сессии.</summary>
    [HttpPost("{processId:guid}/debug/{sessionId:guid}/complete")]
    [ProducesResponseType(typeof(BpmDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDebugSessionDto>> CompleteDebugTask(
        Guid processId,
        Guid sessionId,
        CancellationToken ct)
        => Ok(await _service.StepDebugSessionAsync(processId, sessionId, "complete", ct));

    /// <summary>Пропускает текущую задачу в debug-сессии.</summary>
    [HttpPost("{processId:guid}/debug/{sessionId:guid}/skip")]
    [ProducesResponseType(typeof(BpmDebugSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDebugSessionDto>> SkipDebugTask(
        Guid processId,
        Guid sessionId,
        CancellationToken ct)
        => Ok(await _service.StepDebugSessionAsync(processId, sessionId, "skip", ct));

    /// <summary>Генерирует PDF-регламент процесса по активной версии.</summary>
    [HttpGet("{processId:guid}/document")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Document(
        Guid processId,
        CancellationToken ct)
    {
        var result = await _service.GenerateDocumentAsync(processId, ct);
        return File(result.Content, "application/pdf", result.FileName);
    }

    // ─── Вспомогательные методы ───

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
