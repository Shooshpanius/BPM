using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>API управления шаблонами документов процессов (FR-BPM-02.6).</summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class BpmDocTemplateController : ControllerBase
{
    private readonly AppDbContext _db;

    public BpmDocTemplateController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Возвращает список шаблонов документов (без файлового содержимого).</summary>
    [HttpGet("api/admin/doc-templates")]
    [ProducesResponseType(typeof(IReadOnlyList<DocTemplateListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocTemplateListItemDto>>> GetTemplates(CancellationToken ct)
    {
        var templates = await _db.BpmDocTemplates
            .AsNoTracking()
            .OrderByDescending(t => t.UploadedAt)
            .Select(t => new DocTemplateListItemDto(t.Id, t.Name, t.FileName, t.UploadedAt))
            .ToListAsync(ct);

        return Ok(templates);
    }

    /// <summary>Загружает новый шаблон документа (.docx).</summary>
    [HttpPost("api/admin/doc-templates")]
    [ProducesResponseType(typeof(DocTemplateListItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocTemplateListItemDto>> UploadTemplate(
        IFormFile file,
        [FromForm] string name,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Файл шаблона не может быть пустым" });

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Наименование шаблона не может быть пустым" });

        var userId = GetCurrentUserId() ?? Guid.Empty;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        var now = DateTimeOffset.UtcNow;
        var template = new BpmDocTemplate
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            FileName = file.FileName,
            FileContent = ms.ToArray(),
            UploadedByUserId = userId,
            UploadedAt = now,
            UpdatedAt = now
        };

        _db.BpmDocTemplates.Add(template);
        await _db.SaveChangesAsync(ct);

        var dto = new DocTemplateListItemDto(template.Id, template.Name, template.FileName, template.UploadedAt);
        return CreatedAtAction(nameof(DownloadTemplate), new { id = template.Id }, dto);
    }

    /// <summary>Удаляет шаблон документа.</summary>
    [HttpDelete("api/admin/doc-templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        var template = await _db.BpmDocTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Шаблон документа {id} не найден");

        _db.BpmDocTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Скачивает файл шаблона документа.</summary>
    [HttpGet("api/admin/doc-templates/{id:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTemplate(Guid id, CancellationToken ct)
    {
        var template = await _db.BpmDocTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException($"Шаблон документа {id} не найден");

        return File(
            template.FileContent,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            template.FileName);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
