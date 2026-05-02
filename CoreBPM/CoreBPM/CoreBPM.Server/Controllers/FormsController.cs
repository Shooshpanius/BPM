using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Exceptions;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления формами задач (FR-BPM-01.4).</summary>
[ApiController]
[Route("api/forms")]
[Authorize]
public class FormsController : ControllerBase
{
    private readonly IFormService _service;

    public FormsController(IFormService service)
    {
        _service = service;
    }

    // ─── CRUD форм ────────────────────────────────────────────────────────────

    /// <summary>Возвращает список форм (опционально фильтр по processId).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FormListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FormListItemDto>>> GetAll(
        [FromQuery] Guid? processId,
        CancellationToken ct)
        => Ok(await _service.GetFormsAsync(processId, ct));

    /// <summary>Возвращает форму по идентификатору.</summary>
    [HttpGet("{formId:guid}")]
    [ProducesResponseType(typeof(FormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDto>> GetById(Guid formId, CancellationToken ct)
        => Ok(await _service.GetFormByIdAsync(formId, ct));

    /// <summary>Создаёт новую форму с пустым черновиком версии 1.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FormDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FormDto>> Create(
        [FromBody] CreateFormRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateFormAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { formId = result.Id }, result);
    }

    /// <summary>Обновляет метаданные формы.</summary>
    [HttpPut("{formId:guid}")]
    [ProducesResponseType(typeof(FormDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormDto>> Update(
        Guid formId,
        [FromBody] UpdateFormRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateFormAsync(formId, request, ct));

    /// <summary>Удаляет форму (запрещено при наличии опубликованных версий).</summary>
    [HttpDelete("{formId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid formId, CancellationToken ct)
    {
        await _service.DeleteFormAsync(formId, ct);
        return NoContent();
    }

    // ─── Версионирование ──────────────────────────────────────────────────────

    /// <summary>Возвращает историю версий формы.</summary>
    [HttpGet("{formId:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<FormVersionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FormVersionInfoDto>>> GetVersions(
        Guid formId, CancellationToken ct)
        => Ok(await _service.GetVersionsAsync(formId, ct));

    /// <summary>Возвращает указанную версию формы со схемой.</summary>
    [HttpGet("{formId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType(typeof(FormVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormVersionDto>> GetVersion(
        Guid formId, Guid versionId, CancellationToken ct)
        => Ok(await _service.GetVersionAsync(formId, versionId, ct));

    /// <summary>Сохраняет новый черновик формы.</summary>
    [HttpPost("{formId:guid}/versions")]
    [ProducesResponseType(typeof(FormVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormVersionDto>> SaveDraft(
        Guid formId,
        [FromBody] SaveFormVersionRequest request,
        CancellationToken ct)
    {
        var result = await _service.SaveDraftAsync(formId, request, ct);
        return CreatedAtAction(nameof(GetVersion), new { formId, versionId = result.Id }, result);
    }

    /// <summary>Публикует указанную версию формы.</summary>
    [HttpPost("{formId:guid}/versions/{versionId:guid}/publish")]
    [ProducesResponseType(typeof(FormVersionInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormVersionInfoDto>> Publish(
        Guid formId, Guid versionId, CancellationToken ct)
        => Ok(await _service.PublishVersionAsync(formId, versionId, ct));

    /// <summary>Откатывает к версии: создаёт новый черновик-копию.</summary>
    [HttpPost("{formId:guid}/versions/{versionId:guid}/rollback")]
    [ProducesResponseType(typeof(FormVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormVersionDto>> Rollback(
        Guid formId, Guid versionId, CancellationToken ct)
    {
        var result = await _service.RollbackVersionAsync(formId, versionId, ct);
        return CreatedAtAction(nameof(GetVersion), new { formId, versionId = result.Id }, result);
    }

    /// <summary>Экспортирует версию формы как JSON-файл.</summary>
    [HttpGet("{formId:guid}/versions/{versionId:guid}/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportVersion(Guid formId, Guid versionId, CancellationToken ct)
    {
        var data = await _service.ExportVersionAsync(formId, versionId, ct);
        return File(data, "application/json", $"form-{formId}-v{versionId}.json");
    }

    /// <summary>Импортирует JSON-данные как новый черновик версии формы. Принимает JSON-тело или multipart-файл.</summary>
    [HttpPost("{formId:guid}/versions/import")]
    [ProducesResponseType(typeof(FormVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FormVersionDto>> ImportVersion(
        Guid formId,
        CancellationToken ct)
    {
        byte[] data;

        if (Request.ContentType?.Contains("multipart/form-data") == true)
        {
            var file = Request.Form.Files.FirstOrDefault()
                ?? throw new ValidationException("Файл не передан");
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            data = ms.ToArray();
        }
        else
        {
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms, ct);
            data = ms.ToArray();
        }

        var result = await _service.ImportVersionAsync(formId, data, ct);
        return CreatedAtAction(nameof(GetVersion), new { formId, versionId = result.Id }, result);
    }
}
