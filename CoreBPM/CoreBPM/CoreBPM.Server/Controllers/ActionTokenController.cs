using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Controllers;

/// <summary>Обработка одноразовых токенов действий из actionable email (FR-MSG-02.1).</summary>
[ApiController]
[Route("api/action")]
public class ActionTokenController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActionTokenController(AppDbContext db) => _db = db;

    /// <summary>
    /// Обработать токен действия из ссылки в письме.
    /// Перенаправляет пользователя на нужную страницу с предзаполненным действием.
    /// </summary>
    [HttpGet("{token:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleToken(Guid token, CancellationToken ct)
    {
        var entry = await _db.NotifyActionTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (entry is null)
            return Redirect("/notifications?error=token_not_found");

        if (entry.UsedAt is not null)
            return Redirect("/notifications?error=token_used");

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
            return Redirect("/notifications?error=token_expired");

        // Отмечаем токен как использованный
        entry.UsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Перенаправляем на соответствующую страницу с action-параметром
        var redirectUrl = BuildRedirectUrl(entry.ActionType, entry.EntityId);
        return Redirect(redirectUrl);
    }

    /// <summary>Проверить статус токена (для фронтенда).</summary>
    [HttpGet("{token:guid}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(Guid token, CancellationToken ct)
    {
        var entry = await _db.NotifyActionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (entry is null)
            return Ok(new { valid = false, reason = "not_found" });
        if (entry.UsedAt is not null)
            return Ok(new { valid = false, reason = "used", usedAt = entry.UsedAt });
        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
            return Ok(new { valid = false, reason = "expired" });

        return Ok(new
        {
            valid = true,
            actionType = entry.ActionType,
            eventType = entry.EventType,
            entityId = entry.EntityId,
            expiresAt = entry.ExpiresAt,
        });
    }

    private static string BuildRedirectUrl(string actionType, Guid? entityId) => actionType switch
    {
        "Approve" when entityId.HasValue => $"/tasks/{entityId}?action=approve",
        "Reject" when entityId.HasValue => $"/tasks/{entityId}?action=reject",
        "Open" when entityId.HasValue => $"/tasks/{entityId}",
        _ => "/notifications",
    };
}
