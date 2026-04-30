using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления C#-сценариями версий процессов (FR-BPM-01.7).</summary>
[ApiController]
[Authorize]
public class BpmScriptsController : ControllerBase
{
    private readonly IBpmScriptService _service;

    public BpmScriptsController(IBpmScriptService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список процессов / версий с информацией о сценариях организации.</summary>
    [HttpGet("api/bpm/organizations/{organizationId}/scripts")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmProcessVersionScriptInfoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmProcessVersionScriptInfoDto>>> ListProcessVersionScripts(
        Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.ListProcessVersionScriptsAsync(organizationId, ct));

    /// <summary>Возвращает модуль сценариев выбранной версии процесса (создаётся лениво).</summary>
    [HttpGet("api/bpm/processes/{processId}/versions/{versionId}/scripts")]
    [ProducesResponseType(typeof(BpmScriptModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmScriptModuleDto>> GetScript(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
        => Ok(await _service.GetScriptAsync(processId, versionId, ct));

    /// <summary>Сохраняет (черновик) тело C#-сценария версии процесса.</summary>
    [HttpPut("api/bpm/processes/{processId}/versions/{versionId}/scripts")]
    [ProducesResponseType(typeof(BpmScriptModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmScriptModuleDto>> SaveScript(
        Guid processId,
        Guid versionId,
        [FromBody] SaveScriptModuleRequest request,
        CancellationToken ct)
        => Ok(await _service.SaveScriptAsync(processId, versionId, request, ct));

    /// <summary>Публикует сценарий версии — изменения применяются ко всем запущенным экземплярам этой версии.</summary>
    [HttpPost("api/bpm/processes/{processId}/versions/{versionId}/scripts/publish")]
    [ProducesResponseType(typeof(BpmScriptModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmScriptModuleDto>> PublishScript(
        Guid processId,
        Guid versionId,
        CancellationToken ct)
        => Ok(await _service.PublishScriptAsync(processId, versionId, ct));
}
