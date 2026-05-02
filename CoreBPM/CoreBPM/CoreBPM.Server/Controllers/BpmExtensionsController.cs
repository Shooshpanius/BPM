using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Exceptions;
namespace CoreBPM.Server.Controllers;

/// <summary>API управления пользовательскими расширениями палитры дизайнера (FR-BPM-01.7).</summary>
[ApiController]
[Route("api/bpm/designer/extensions")]
[Authorize]
public class BpmExtensionsController : ControllerBase
{
    private readonly IBpmExtensionService _service;

    public BpmExtensionsController(IBpmExtensionService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список расширений организации, отсортированных по папке и названию.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmDesignerExtensionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmDesignerExtensionDto>>> List(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.ListAsync(organizationId, ct));

    /// <summary>Возвращает расширение по идентификатору.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BpmDesignerExtensionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDesignerExtensionDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    /// <summary>Создаёт новое расширение.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BpmDesignerExtensionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BpmDesignerExtensionDto>> Create(
        [FromBody] CreateDesignerExtensionRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var dto = await _service.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Обновляет расширение.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(BpmDesignerExtensionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDesignerExtensionDto>> Update(
        Guid id,
        [FromBody] UpdateDesignerExtensionRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>Мягко удаляет расширение.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Публикует расширение — делает его доступным в палитре дизайнера.</summary>
    [HttpPost("{id}/publish")]
    [ProducesResponseType(typeof(BpmDesignerExtensionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDesignerExtensionDto>> Publish(Guid id, CancellationToken ct)
        => Ok(await _service.PublishAsync(id, ct));

    /// <summary>Создаёт копию расширения (черновик) как основу для нового.</summary>
    [HttpPost("{id}/copy")]
    [ProducesResponseType(typeof(BpmDesignerExtensionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmDesignerExtensionDto>> Copy(Guid id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var dto = await _service.CopyAsync(id, userId, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Экспортирует все расширения организации в JSON-файл.</summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] Guid organizationId, CancellationToken ct)
    {
        var data = await _service.ExportAsync(organizationId, ct);
        return File(data, "application/json", $"extensions-{organizationId}.json");
    }

    /// <summary>Импортирует расширения из JSON-файла или JSON-тела.</summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmDesignerExtensionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BpmDesignerExtensionDto>>> Import(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
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

        var result = await _service.ImportAsync(organizationId, data, userId, ct);
        return Ok(result);
    }

    // ─── Вспомогательные методы ─────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }
}
