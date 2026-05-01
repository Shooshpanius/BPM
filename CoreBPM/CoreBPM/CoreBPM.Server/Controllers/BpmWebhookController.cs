using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Публичный endpoint для запуска процессов через внешний REST-вебхук.
/// Не требует JWT-авторизации — защищён секретным ключом в URL.</summary>
[ApiController]
[Route("api/bpm/webhooks")]
public class BpmWebhookController : ControllerBase
{
    private readonly IBpmInstanceService _service;

    public BpmWebhookController(IBpmInstanceService service)
    {
        _service = service;
    }

    /// <summary>Запускает экземпляр процесса через вебхук.
    /// <para>Внешняя система вызывает этот endpoint с ключом вебхука, полученным из настроек процесса.
    /// Тело запроса маппируется в переменные создаваемого экземпляра.</para>
    /// </summary>
    [HttpPost("{webhookKey}")]
    [ProducesResponseType(typeof(BpmInstanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmInstanceDto>> Trigger(
        string webhookKey,
        [FromBody] WebhookLaunchRequest request,
        CancellationToken ct)
    {
        var instance = await _service.CreateInstanceViaWebhookAsync(webhookKey, request, ct);
        return StatusCode(StatusCodes.Status201Created, instance);
    }
}
