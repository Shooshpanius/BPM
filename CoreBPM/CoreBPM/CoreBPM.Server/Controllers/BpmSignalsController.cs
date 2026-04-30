using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>
/// CRUD реестра сигналов BPMN.
/// </summary>
[ApiController]
[Route("api/bpm/signals")]
[Authorize]
public class BpmSignalsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BpmSignalsController(AppDbContext db)
    {
        _db = db;
    }

    // ─── DTO ─────────────────────────────────────────────────────────────────

    public record BpmSignalDto(Guid Id, string Name, string Code, string? Description, DateTime CreatedAt, DateTime UpdatedAt);
    public record CreateSignalRequest(string Name, string? Code, string? Description);
    public record UpdateSignalRequest(string Name, string Code, string? Description);

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static string GenerateCode(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToUpperInvariant().Replace(' ', '_'), "[^A-Z0-9_]", "");

    private Guid GetOrgId()
    {
        var claim = User.Claims.FirstOrDefault(c => c.Type == "organizationId" || c.Type == "org")?.Value;
        if (claim != null && Guid.TryParse(claim, out var orgId)) return orgId;
        // Fallback через аккаунт
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var account = _db.AuthAccounts.AsNoTracking().Include(a => a.User).ThenInclude(u => u!.Employees).FirstOrDefault(a => a.UserId == userId);
        return account?.User?.Employees.FirstOrDefault()?.OrganizationId ?? Guid.Empty;
    }

    private static BpmSignalDto ToDto(BpmSignal s) => new(s.Id, s.Name, s.Code, s.Description, s.CreatedAt, s.UpdatedAt);

    // ─── Эндпоинты ────────────────────────────────────────────────────────────

    /// <summary>Список сигналов организации.</summary>
    [HttpGet]
    public async Task<ActionResult<List<BpmSignalDto>>> GetSignals([FromQuery] Guid? organizationId)
    {
        var orgId = organizationId ?? GetOrgId();
        var signals = await _db.BpmSignals
            .AsNoTracking()
            .Where(s => s.OrganizationId == orgId)
            .OrderBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync();
        return Ok(signals);
    }

    /// <summary>Получить сигнал по ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BpmSignalDto>> GetSignal(Guid id)
    {
        var signal = await _db.BpmSignals.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (signal == null) return NotFound(new { error = "Сигнал не найден" });
        return Ok(ToDto(signal));
    }

    /// <summary>Создать сигнал.</summary>
    [HttpPost]
    public async Task<ActionResult<BpmSignalDto>> CreateSignal([FromBody] CreateSignalRequest req, [FromQuery] Guid? organizationId)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Название обязательно" });

        var orgId = organizationId ?? GetOrgId();
        var code = string.IsNullOrWhiteSpace(req.Code) ? GenerateCode(req.Name) : req.Code.Trim();

        if (string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Не удалось сгенерировать код из названия" });

        if (await _db.BpmSignals.AnyAsync(s => s.OrganizationId == orgId && s.Code == code))
            return Conflict(new { error = $"Сигнал с кодом '{code}' уже существует" });

        var signal = new BpmSignal
        {
            OrganizationId = orgId,
            Name = req.Name.Trim(),
            Code = code,
            Description = req.Description?.Trim(),
        };
        _db.BpmSignals.Add(signal);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetSignal), new { id = signal.Id }, ToDto(signal));
    }

    /// <summary>Обновить сигнал.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BpmSignalDto>> UpdateSignal(Guid id, [FromBody] UpdateSignalRequest req)
    {
        var signal = await _db.BpmSignals.FirstOrDefaultAsync(s => s.Id == id);
        if (signal == null) return NotFound(new { error = "Сигнал не найден" });

        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Название обязательно" });

        var code = req.Code.Trim();
        if (await _db.BpmSignals.AnyAsync(s => s.OrganizationId == signal.OrganizationId && s.Code == code && s.Id != id))
            return Conflict(new { error = $"Сигнал с кодом '{code}' уже существует" });

        signal.Name = req.Name.Trim();
        signal.Code = code;
        signal.Description = req.Description?.Trim();
        signal.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToDto(signal));
    }

    /// <summary>Удалить сигнал.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSignal(Guid id)
    {
        var signal = await _db.BpmSignals.FirstOrDefaultAsync(s => s.Id == id);
        if (signal == null) return NotFound(new { error = "Сигнал не найден" });
        _db.BpmSignals.Remove(signal);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
