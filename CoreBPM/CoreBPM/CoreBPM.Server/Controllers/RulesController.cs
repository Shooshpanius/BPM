using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Rules.DTOs;
using CoreBPM.Server.Application.Rules.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>API для управления DMN-таблицами бизнес-правил.</summary>
[ApiController]
[Route("api/rules")]
[Authorize]
public class RulesController : ControllerBase
{
    private readonly IDmnService _service;

    public RulesController(IDmnService service)
    {
        _service = service;
    }

    // ─── CRUD таблиц ───────────────────────────────────────────────────────────

    /// <summary>Возвращает список всех DMN-таблиц.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DmnTableListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DmnTableListItemDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetTablesAsync(ct));

    /// <summary>Возвращает DMN-таблицу по идентификатору.</summary>
    [HttpGet("{tableId:guid}")]
    [ProducesResponseType(typeof(DmnTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DmnTableDto>> GetById(Guid tableId, CancellationToken ct)
        => Ok(await _service.GetTableByIdAsync(tableId, ct));

    /// <summary>Создаёт новую DMN-таблицу.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DmnTableDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DmnTableDto>> Create(
        [FromBody] CreateDmnTableRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateTableAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { tableId = result.Id }, result);
    }

    /// <summary>Обновляет метаданные DMN-таблицы.</summary>
    [HttpPut("{tableId:guid}")]
    [ProducesResponseType(typeof(DmnTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DmnTableDto>> Update(
        Guid tableId,
        [FromBody] UpdateDmnTableRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateTableAsync(tableId, request, ct));

    /// <summary>Удаляет DMN-таблицу (запрещено при наличии опубликованных версий).</summary>
    [HttpDelete("{tableId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid tableId, CancellationToken ct)
    {
        await _service.DeleteTableAsync(tableId, ct);
        return NoContent();
    }

    // ─── Версионирование ───────────────────────────────────────────────────────

    /// <summary>Возвращает историю версий DMN-таблицы.</summary>
    [HttpGet("{tableId:guid}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<DmnTableVersionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DmnTableVersionInfoDto>>> GetVersions(
        Guid tableId, CancellationToken ct)
        => Ok(await _service.GetVersionsAsync(tableId, ct));

    /// <summary>Возвращает полную схему указанной версии DMN-таблицы.</summary>
    [HttpGet("{tableId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType(typeof(DmnTableVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DmnTableVersionDto>> GetVersion(
        Guid tableId, Guid versionId, CancellationToken ct)
        => Ok(await _service.GetVersionAsync(tableId, versionId, ct));

    /// <summary>Сохраняет новый черновик DMN-таблицы.</summary>
    [HttpPost("{tableId:guid}/versions")]
    [ProducesResponseType(typeof(DmnTableVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DmnTableVersionDto>> SaveDraft(
        Guid tableId,
        [FromBody] SaveDmnTableVersionRequest request,
        CancellationToken ct)
    {
        var result = await _service.SaveDraftAsync(tableId, request, ct);
        return CreatedAtAction(nameof(GetVersion), new { tableId, versionId = result.Id }, result);
    }

    /// <summary>Публикует указанную версию DMN-таблицы.</summary>
    [HttpPost("{tableId:guid}/versions/{versionId:guid}/publish")]
    [ProducesResponseType(typeof(DmnTableVersionInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DmnTableVersionInfoDto>> Publish(
        Guid tableId, Guid versionId, CancellationToken ct)
        => Ok(await _service.PublishVersionAsync(tableId, versionId, ct));

    // ─── Тестирование ─────────────────────────────────────────────────────────

    /// <summary>Тестирует версию DMN-таблицы на заданных входных значениях.</summary>
    [HttpPost("{tableId:guid}/versions/{versionId:guid}/test")]
    [ProducesResponseType(typeof(DmnTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DmnTestResponse>> Test(
        Guid tableId, Guid versionId,
        [FromBody] DmnTestRequest request,
        CancellationToken ct)
        => Ok(await _service.EvaluateAsync(tableId, versionId, request, ct));
}
