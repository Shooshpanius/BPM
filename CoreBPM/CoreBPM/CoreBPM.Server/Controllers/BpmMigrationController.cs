using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;

namespace CoreBPM.Server.Controllers;

/// <summary>API пакетов миграции версий экземпляров процессов (FR-BPM-02.7).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class BpmMigrationController : ControllerBase
{
    private readonly IBpmMigrationService _service;

    public BpmMigrationController(IBpmMigrationService service)
    {
        _service = service;
    }

    /// <summary>Возвращает список пакетов миграции.</summary>
    [HttpGet("api/bpm/migration-packages")]
    [ProducesResponseType(typeof(IReadOnlyList<MigrationPackageListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MigrationPackageListItemDto>>> GetPackages(
        [FromQuery] BpmMigrationPackageStatus? status,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        return Ok(await _service.GetPackagesAsync(status, isActive, page, pageSize, ct));
    }

    /// <summary>Создаёт новый пакет миграции.</summary>
    [HttpPost("api/bpm/migration-packages")]
    [ProducesResponseType(typeof(MigrationPackageDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<MigrationPackageDetailDto>> CreatePackage(
        [FromBody] CreateMigrationPackageRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId() ?? Guid.Empty;
        var result = await _service.CreatePackageAsync(userId, request, ct);
        return CreatedAtAction(nameof(GetPackage), new { id = result.Id }, result);
    }

    /// <summary>Возвращает детали пакета миграции.</summary>
    [HttpGet("api/bpm/migration-packages/{id:guid}")]
    [ProducesResponseType(typeof(MigrationPackageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MigrationPackageDetailDto>> GetPackage(
        Guid id,
        CancellationToken ct = default)
    {
        return Ok(await _service.GetPackageAsync(id, ct));
    }

    /// <summary>Запускает выполнение пакета миграции.</summary>
    [HttpPost("api/bpm/migration-packages/{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartPackage(Guid id, CancellationToken ct = default)
    {
        await _service.StartPackageAsync(id, ct);
        return NoContent();
    }

    /// <summary>Отменяет пакет миграции.</summary>
    [HttpPost("api/bpm/migration-packages/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelPackage(Guid id, CancellationToken ct = default)
    {
        await _service.CancelPackageAsync(id, ct);
        return NoContent();
    }

    /// <summary>Возвращает список элементов пакета миграции.</summary>
    [HttpGet("api/bpm/migration-packages/{id:guid}/items")]
    [ProducesResponseType(typeof(IReadOnlyList<MigrationItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MigrationItemDto>>> GetPackageItems(
        Guid id,
        [FromQuery] BpmMigrationItemStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        return Ok(await _service.GetPackageItemsAsync(id, status, page, pageSize, ct));
    }

    /// <summary>Отмечает элемент пакета как обработанный вручную.</summary>
    [HttpPost("api/bpm/migration-packages/{id:guid}/items/{itemId:guid}/manual")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ManualMigrateItem(
        Guid id,
        Guid itemId,
        [FromBody] ManualMigrateItemRequest request,
        CancellationToken ct = default)
    {
        await _service.ManualMigrateItemAsync(id, itemId, request, ct);
        return NoContent();
    }

    /// <summary>Экспортирует пакет миграции в JSON-файл для переноса между средами.</summary>
    [HttpGet("api/bpm/migration-packages/{id:guid}/export")]
    [ProducesResponseType(typeof(MigrationPackageExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MigrationPackageExportDto>> ExportPackage(
        Guid id,
        CancellationToken ct = default)
    {
        var export = await _service.ExportPackageAsync(id, ct);
        var fileName = $"migration-package-{id:N}.json";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        return Ok(export);
    }

    /// <summary>Импортирует пакет миграции из JSON-тела запроса и создаёт новый пакет в статусе New.</summary>
    [HttpPost("api/bpm/migration-packages/import")]
    [ProducesResponseType(typeof(MigrationPackageDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MigrationPackageDetailDto>> ImportPackage(
        [FromBody] MigrationPackageExportDto export,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId() ?? Guid.Empty;
        var result = await _service.ImportPackageAsync(userId, export, ct);
        return CreatedAtAction(nameof(GetPackage), new { id = result.Id }, result);
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
