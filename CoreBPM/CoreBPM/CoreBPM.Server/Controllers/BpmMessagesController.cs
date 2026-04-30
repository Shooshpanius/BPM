using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// CRUD реестра сообщений BPMN.
/// </summary>
[ApiController]
[Route("api/bpm/messages")]
[Authorize]
public class BpmMessagesController : ControllerBase
{
    private readonly AppDbContext _db;

    public BpmMessagesController(AppDbContext db)
    {
        _db = db;
    }

    // ─── DTO ─────────────────────────────────────────────────────────────────

    public record BpmMessageDto(Guid Id, string Name, string Code, string? Description, DateTime CreatedAt, DateTime UpdatedAt);
    public record CreateMessageRequest(string Name, string? Code, string? Description);
    public record UpdateMessageRequest(string Name, string Code, string? Description);

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static string GenerateCode(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToUpperInvariant().Replace(' ', '_'), "[^A-Z0-9_]", "");

    private Guid GetOrgId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "organizationId" || c.Type == "org")?.Value;
        if (claim != null && Guid.TryParse(claim, out var orgId)) return orgId;
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = _db.AuthAccounts.AsNoTracking().Include(a => a.User).ThenInclude(u => u!.Employees).FirstOrDefault(a => a.UserId == userId);
        return account?.User?.Employees.FirstOrDefault()?.OrganizationId ?? Guid.Empty;
    }

    private static BpmMessageDto ToDto(BpmMessage m) => new(m.Id, m.Name, m.Code, m.Description, m.CreatedAt, m.UpdatedAt);

    // ─── Эндпоинты ────────────────────────────────────────────────────────────

    /// <summary>Список сообщений организации.</summary>
    [HttpGet]
    public async Task<ActionResult<List<BpmMessageDto>>> GetMessages([FromQuery] Guid? organizationId)
    {
        var orgId = organizationId ?? GetOrgId();
        var messages = await _db.BpmMessages
            .AsNoTracking()
            .Where(m => m.OrganizationId == orgId)
            .OrderBy(m => m.Name)
            .Select(m => ToDto(m))
            .ToListAsync();
        return Ok(messages);
    }

    /// <summary>Получить сообщение по ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BpmMessageDto>> GetMessage(Guid id)
    {
        var message = await _db.BpmMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (message == null) return NotFound(new { error = "Сообщение не найдено" });
        return Ok(ToDto(message));
    }

    /// <summary>Создать сообщение.</summary>
    [HttpPost]
    public async Task<ActionResult<BpmMessageDto>> CreateMessage([FromBody] CreateMessageRequest req, [FromQuery] Guid? organizationId)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Название обязательно" });

        var orgId = organizationId ?? GetOrgId();
        var code = string.IsNullOrWhiteSpace(req.Code) ? GenerateCode(req.Name) : req.Code.Trim();

        if (string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Не удалось сгенерировать код из названия" });

        if (await _db.BpmMessages.AnyAsync(m => m.OrganizationId == orgId && m.Code == code))
            return Conflict(new { error = $"Сообщение с кодом '{code}' уже существует" });

        var message = new BpmMessage
        {
            OrganizationId = orgId,
            Name = req.Name.Trim(),
            Code = code,
            Description = req.Description?.Trim(),
        };
        _db.BpmMessages.Add(message);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMessage), new { id = message.Id }, ToDto(message));
    }

    /// <summary>Обновить сообщение.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BpmMessageDto>> UpdateMessage(Guid id, [FromBody] UpdateMessageRequest req)
    {
        var message = await _db.BpmMessages.FirstOrDefaultAsync(m => m.Id == id);
        if (message == null) return NotFound(new { error = "Сообщение не найдено" });

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Название обязательно" });

        var code = req.Code.Trim();
        if (await _db.BpmMessages.AnyAsync(m => m.OrganizationId == message.OrganizationId && m.Code == code && m.Id != id))
            return Conflict(new { error = $"Сообщение с кодом '{code}' уже существует" });

        message.Name = req.Name.Trim();
        message.Code = code;
        message.Description = req.Description?.Trim();
        message.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(message));
    }

    /// <summary>Удалить сообщение.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        var message = await _db.BpmMessages.FirstOrDefaultAsync(m => m.Id == id);
        if (message == null) return NotFound(new { error = "Сообщение не найдено" });
        _db.BpmMessages.Remove(message);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
