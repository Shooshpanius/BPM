using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Управление шаблонами email-уведомлений (FR-MSG-02.1).</summary>
[ApiController]
[Route("api/admin/email-templates")]
[Authorize(Roles = "Admin")]
public class AdminEmailTemplatesController : ControllerBase
{
    private readonly IEmailTemplateService _service;

    public AdminEmailTemplatesController(IEmailTemplateService service) => _service = service;

    /// <summary>Получить все шаблоны email-уведомлений.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _service.GetAllAsync(ct);
        return Ok(result);
    }

    /// <summary>Получить шаблон по типу события.</summary>
    [HttpGet("{eventType}")]
    public async Task<IActionResult> GetByEventType(string eventType, CancellationToken ct)
    {
        var result = await _service.GetByEventTypeAsync(eventType, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Создать или обновить шаблон email-уведомления.</summary>
    [HttpPut("{eventType}")]
    public async Task<IActionResult> Upsert(string eventType, [FromBody] UpsertEmailTemplateRequest request, CancellationToken ct)
    {
        if (request.EventType != eventType)
            return BadRequest(new { error = "Тип события в теле не совпадает с URL" });
        var result = await _service.UpsertAsync(request, ct);
        return Ok(result);
    }

    /// <summary>Удалить шаблон (сброс к дефолтному).</summary>
    [HttpDelete("{eventType}")]
    public async Task<IActionResult> Delete(string eventType, CancellationToken ct)
    {
        await _service.DeleteAsync(eventType, ct);
        return NoContent();
    }

    /// <summary>Предпросмотр рендеринга шаблона.</summary>
    [HttpPost("{eventType}/preview")]
    public async Task<IActionResult> Preview(string eventType, [FromBody] PreviewEmailRequest request, CancellationToken ct)
    {
        var (subject, html) = await _service.RenderAsync(
            eventType, request.Title, request.Body, request.Link, null, ct);
        return Ok(new { subject, html });
    }
}

/// <summary>Запрос предпросмотра шаблона.</summary>
public sealed record PreviewEmailRequest(string Title, string Body, string? Link);
