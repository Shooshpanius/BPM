using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер страницы компании (FR-ORG-03).</summary>
[ApiController]
[Route("api/company")]
[Authorize]
public class CompanyController : ControllerBase
{
    private readonly ICompanyService _service;

    public CompanyController(ICompanyService service)
    {
        _service = service;
    }

    // ─── Сведения о компании ─────────────────────────────────────────────────

    /// <summary>Возвращает сведения о компании.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CompanyInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyInfoDto>> GetInfo(CancellationToken ct)
        => Ok(await _service.GetInfoAsync(ct));

    /// <summary>Обновляет сведения о компании (только Admin).</summary>
    [HttpPut]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CompanyInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompanyInfoDto>> UpdateInfo(
        [FromBody] UpdateCompanyInfoRequest req, CancellationToken ct)
        => Ok(await _service.UpdateInfoAsync(req, ct));

    // ─── Новости ─────────────────────────────────────────────────────────────

    /// <summary>Возвращает список опубликованных новостей компании.</summary>
    [HttpGet("news")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyNewsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyNewsDto>>> GetNews(CancellationToken ct)
        => Ok(await _service.GetNewsAsync(ct));

    /// <summary>Создаёт новость компании (только Admin).</summary>
    [HttpPost("news")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CompanyNewsDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CompanyNewsDto>> CreateNews(
        [FromBody] CreateNewsRequest req, CancellationToken ct)
    {
        var actorId = GetCurrentUserId();
        var dto = await _service.CreateNewsAsync(req, actorId, ct);
        return CreatedAtAction(nameof(GetNews), dto);
    }

    /// <summary>Обновляет новость компании (только Admin).</summary>
    [HttpPut("news/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CompanyNewsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyNewsDto>> UpdateNews(
        Guid id, [FromBody] UpdateNewsRequest req, CancellationToken ct)
        => Ok(await _service.UpdateNewsAsync(id, req, ct));

    /// <summary>Удаляет новость компании (только Admin).</summary>
    [HttpDelete("news/{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNews(Guid id, CancellationToken ct)
    {
        await _service.DeleteNewsAsync(id, ct);
        return NoContent();
    }

    // ─── Ссылки ──────────────────────────────────────────────────────────────

    /// <summary>Возвращает список внутренних ссылок компании.</summary>
    [HttpGet("links")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyLinkDto>>> GetLinks(CancellationToken ct)
        => Ok(await _service.GetLinksAsync(ct));

    /// <summary>Заменяет список ссылок компании (только Admin).</summary>
    [HttpPut("links")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyLinkDto>>> UpdateLinks(
        [FromBody] UpdateCompanyLinksRequest req, CancellationToken ct)
        => Ok(await _service.UpdateLinksAsync(req, ct));

    // ── Вспомогательный метод ────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Не удалось определить идентификатор пользователя.");
        return Guid.Parse(raw);
    }
}
