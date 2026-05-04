using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.DTOs;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Управление глобальными шаблонами уведомлений (администратор). FR-MSG-02.2.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/notification-templates")]
public class AdminNotificationTemplatesController : ControllerBase
{
    private readonly INotificationTemplateService _service;

    public AdminNotificationTemplatesController(INotificationTemplateService service)
        => _service = service;

    /// <summary>Получить все шаблоны уведомлений.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationTemplateDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    /// <summary>Получить шаблон по типу события.</summary>
    [HttpGet("{eventType}")]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationTemplateDto>> GetByEventType(
        string eventType, CancellationToken ct)
    {
        var result = await _service.GetByEventTypeAsync(eventType, ct);
        if (result is null) return NotFound(new { error = $"Шаблон для события '{eventType}' не найден." });
        return Ok(result);
    }

    /// <summary>Создать или обновить шаблон для типа события.</summary>
    [HttpPut("{eventType}")]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationTemplateDto>> Upsert(
        string eventType, [FromBody] UpsertNotificationTemplateRequest req, CancellationToken ct)
        => Ok(await _service.UpsertAsync(eventType, req, ct));

    /// <summary>Удалить шаблон по Id.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Предпросмотр: применить переменные к шаблону.</summary>
    [HttpPost("{eventType}/preview")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        string eventType,
        [FromBody] Dictionary<string, string> variables,
        CancellationToken ct)
    {
        var tmpl = await _service.GetByEventTypeAsync(eventType, ct);
        if (tmpl is null) return NotFound(new { error = $"Шаблон для события '{eventType}' не найден." });

        return Ok(new
        {
            subject = _service.Render(tmpl.EmailSubjectTemplate, variables),
            body = _service.Render(tmpl.EmailBodyTemplate, variables),
            shortText = _service.Render(tmpl.ShortTemplate, variables),
        });
    }
}
