using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для работы с токенами выполнения BPMN.</summary>
[ApiController]
[Authorize]
public class BpmTokensController : ControllerBase
{
    private readonly IBpmExecutionEngine _engine;

    public BpmTokensController(IBpmExecutionEngine engine)
    {
        _engine = engine;
    }

    /// <summary>Возвращает список активных токенов экземпляра (для карты процесса).</summary>
    [HttpGet("api/bpm/instances/{instanceId}/tokens")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmTokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BpmTokenDto>>> GetTokens(
        Guid instanceId,
        CancellationToken ct)
        => Ok(await _engine.GetTokensAsync(instanceId, ct));

    /// <summary>Завершает UserTask/ReceiveTask с передачей выходных переменных.</summary>
    [HttpPost("api/bpm/instances/{instanceId}/tokens/{elementId}/complete")]
    [ProducesResponseType(typeof(BpmTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmTokenDto>> CompleteToken(
        Guid instanceId,
        string elementId,
        [FromBody] CompleteUserTaskRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _engine.CompleteUserTaskAsync(
            instanceId,
            elementId,
            userId.Value,
            request.OutputVariables,
            ct);
        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
