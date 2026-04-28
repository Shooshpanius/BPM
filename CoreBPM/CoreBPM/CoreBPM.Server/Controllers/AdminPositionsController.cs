using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер административного управления должностями.</summary>
[ApiController]
[Route("api/admin/positions")]
[Authorize(Roles = "Admin")]
public class AdminPositionsController : ControllerBase
{
    private readonly IOrgPositionsService _service;

    public AdminPositionsController(IOrgPositionsService service)
    {
        _service = service;
    }

    /// <summary>Создаёт новую должность.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionResponse>> Create(
        [FromBody] CreatePositionRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreatePositionAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { positionId = result.Id }, result);
    }

    /// <summary>Возвращает должность по идентификатору (административный доступ).</summary>
    [HttpGet("{positionId:guid}")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionResponse>> GetById(Guid positionId, CancellationToken ct)
        => Ok(await _service.GetPositionByIdAsync(positionId, ct));

    /// <summary>Обновляет данные должности.</summary>
    [HttpPut("{positionId:guid}")]
    [ProducesResponseType(typeof(PositionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionResponse>> Update(
        Guid positionId,
        [FromBody] UpdatePositionRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdatePositionAsync(positionId, request, ct));

    /// <summary>Архивирует должность (мягкое удаление). Запрет при наличии действующих назначений.</summary>
    [HttpDelete("{positionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid positionId, CancellationToken ct)
    {
        await _service.ArchivePositionAsync(positionId, ct);
        return NoContent();
    }

    /// <summary>Возвращает матрицу ролей, закреплённых за должностью.</summary>
    [HttpGet("{positionId:guid}/role-mappings")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionRoleMappingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PositionRoleMappingResponse>>> GetRoleMappings(
        Guid positionId,
        CancellationToken ct)
        => Ok(await _service.GetRoleMappingsAsync(positionId, ct));

    /// <summary>Полностью заменяет матрицу ролей должности.</summary>
    [HttpPut("{positionId:guid}/role-mappings")]
    [ProducesResponseType(typeof(IReadOnlyList<PositionRoleMappingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PositionRoleMappingResponse>>> SetRoleMappings(
        Guid positionId,
        [FromBody] SetPositionRoleMappingsRequest request,
        CancellationToken ct)
        => Ok(await _service.SetRoleMappingsAsync(positionId, request, ct));

    /// <summary>Загружает файл должностной инструкции.</summary>
    [HttpPost("{positionId:guid}/attachments")]
    [ProducesResponseType(typeof(PositionAttachmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PositionAttachmentResponse>> AddAttachment(
        Guid positionId,
        IFormFile file,
        [FromForm] string? description,
        CancellationToken ct)
    {
        var result = await _service.AddAttachmentAsync(positionId, file, description, ct);
        return CreatedAtAction(nameof(GetById), new { positionId }, result);
    }

    /// <summary>Удаляет вложение должности.</summary>
    [HttpDelete("{positionId:guid}/attachments/{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachment(Guid positionId, Guid attachmentId, CancellationToken ct)
    {
        await _service.DeleteAttachmentAsync(attachmentId, ct);
        return NoContent();
    }
}
