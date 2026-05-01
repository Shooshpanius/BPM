using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для отправки BPMN-событий (сигналы и сообщения).</summary>
[ApiController]
[Authorize]
public class BpmEventsController : ControllerBase
{
    private readonly IBpmExecutionEngine _engine;

    public BpmEventsController(IBpmExecutionEngine engine)
    {
        _engine = engine;
    }

    /// <summary>Рассылает сигнал всем экземплярам процессов, ожидающим данный сигнал.</summary>
    [HttpPost("api/events/signals/{signalCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendSignal(string signalCode, CancellationToken ct)
    {
        await _engine.SendSignalAsync(signalCode, ct);
        return NoContent();
    }

    /// <summary>Доставляет сообщение экземплярам, ожидающим данный код сообщения.</summary>
    [HttpPost("api/events/messages/{messageCode}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SendMessage(
        string messageCode,
        [FromBody] SendMessageRequest request,
        CancellationToken ct)
    {
        await _engine.SendMessageAsync(messageCode, request.CorrelationKey, ct);
        return NoContent();
    }
}
