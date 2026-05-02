using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления глобальными C#-модулями (FR-BPM-01.7).</summary>
[ApiController]
[Route("api/bpm/designer/global-modules")]
[Authorize]
public class BpmGlobalModulesController : ControllerBase
{
    private readonly IBpmGlobalModuleService _service;

    public BpmGlobalModulesController(IBpmGlobalModuleService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список глобальных модулей организации.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BpmGlobalModuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BpmGlobalModuleDto>>> List(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
        => Ok(await _service.ListAsync(organizationId, ct));

    /// <summary>Возвращает глобальный модуль по идентификатору.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BpmGlobalModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmGlobalModuleDto>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    /// <summary>Создаёт новый глобальный модуль.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BpmGlobalModuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BpmGlobalModuleDto>> Create(
        [FromBody] CreateGlobalModuleRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var dto = await _service.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    /// <summary>Обновляет название и описание глобального модуля.</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(BpmGlobalModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmGlobalModuleDto>> Update(
        Guid id,
        [FromBody] UpdateGlobalModuleRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    /// <summary>Мягко удаляет глобальный модуль.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Публикует глобальный модуль — делает его доступным в среде выполнения сценариев.</summary>
    [HttpPost("{id}/publish")]
    [ProducesResponseType(typeof(BpmGlobalModuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmGlobalModuleDto>> Publish(Guid id, CancellationToken ct)
        => Ok(await _service.PublishAsync(id, ct));

    // ─── Файлы модуля ────────────────────────────────────────────────────────

    /// <summary>Возвращает список файлов глобального модуля.</summary>
    [HttpGet("{id}/files")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmGlobalModuleFileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<BpmGlobalModuleFileDto>>> ListFiles(
        Guid id,
        CancellationToken ct)
        => Ok(await _service.ListFilesAsync(id, ct));

    /// <summary>Добавляет новый файл в глобальный модуль.</summary>
    [HttpPost("{id}/files")]
    [ProducesResponseType(typeof(BpmGlobalModuleFileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmGlobalModuleFileDto>> AddFile(
        Guid id,
        [FromBody] CreateGlobalModuleFileRequest request,
        CancellationToken ct)
    {
        var dto = await _service.AddFileAsync(id, request, ct);
        return CreatedAtAction(nameof(ListFiles), new { id }, dto);
    }

    /// <summary>Обновляет файл глобального модуля.</summary>
    [HttpPut("{id}/files/{fileId}")]
    [ProducesResponseType(typeof(BpmGlobalModuleFileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BpmGlobalModuleFileDto>> UpdateFile(
        Guid id,
        Guid fileId,
        [FromBody] UpdateGlobalModuleFileRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateFileAsync(id, fileId, request, ct));

    /// <summary>Удаляет файл из глобального модуля.</summary>
    [HttpDelete("{id}/files/{fileId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(
        Guid id,
        Guid fileId,
        CancellationToken ct)
    {
        await _service.DeleteFileAsync(id, fileId, ct);
        return NoContent();
    }

    /// <summary>Изменяет порядок файлов глобального модуля.</summary>
    [HttpPut("{id}/files/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReorderFiles(
        Guid id,
        [FromBody] ReorderGlobalModuleFilesRequest request,
        CancellationToken ct)
    {
        await _service.ReorderFilesAsync(id, request.OrderedIds, ct);
        return NoContent();
    }

    /// <summary>Экспортирует все глобальные модули организации в JSON-файл.</summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] Guid organizationId, CancellationToken ct)
    {
        var data = await _service.ExportAsync(organizationId, ct);
        return File(data, "application/json", $"global-modules-{organizationId}.json");
    }

    /// <summary>Импортирует глобальные модули из JSON-файла или JSON-тела.</summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(IReadOnlyList<BpmGlobalModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<BpmGlobalModuleDto>>> Import(
        [FromQuery] Guid organizationId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        byte[] data;

        if (Request.ContentType?.Contains("multipart/form-data") == true)
        {
            var file = Request.Form.Files.FirstOrDefault()
                ?? throw new CoreBPM.Server.Exceptions.ValidationException("Файл не передан");
            using var ms = new System.IO.MemoryStream();
            await file.CopyToAsync(ms, ct);
            data = ms.ToArray();
        }
        else
        {
            using var ms = new System.IO.MemoryStream();
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
