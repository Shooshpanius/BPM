using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Notify.Interfaces;

namespace CoreBPM.Server.Controllers;

/// <summary>Управление Web Push подписками пользователя (FR-MSG-02.1).</summary>
[ApiController]
[Route("api")]
[Authorize]
public class PushSubscriptionsController : ControllerBase
{
    private readonly IPushNotificationService _push;

    public PushSubscriptionsController(IPushNotificationService push) => _push = push;

    /// <summary>Получить VAPID публичный ключ (нужен для подписки в браузере).</summary>
    [HttpGet("notifications/vapid-public-key")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVapidPublicKey(CancellationToken ct)
    {
        var key = await _push.GetVapidPublicKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(key))
            return NotFound(new { error = "VAPID не настроен. Обратитесь к администратору." });
        return Ok(new { publicKey = key });
    }

    /// <summary>Зарегистрировать push-подписку браузера.</summary>
    [HttpPost("users/me/push-subscription")]
    public async Task<IActionResult> Register([FromBody] RegisterPushSubscriptionRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Endpoint)
            || string.IsNullOrWhiteSpace(request.P256dh)
            || string.IsNullOrWhiteSpace(request.Auth))
            return BadRequest(new { error = "Endpoint, P256dh и Auth обязательны" });

        var userAgent = Request.Headers.UserAgent.ToString();
        var id = await _push.SaveSubscriptionAsync(
            userId, request.Endpoint, request.P256dh, request.Auth,
            string.IsNullOrWhiteSpace(userAgent) ? null : userAgent[..Math.Min(500, userAgent.Length)],
            ct);

        return Ok(new { id });
    }

    /// <summary>Получить список активных push-подписок текущего пользователя.</summary>
    [HttpGet("users/me/push-subscriptions")]
    public async Task<IActionResult> GetSubscriptions(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await _push.GetUserSubscriptionsAsync(userId, ct);
        return Ok(result);
    }

    /// <summary>Удалить push-подписку (отписать браузер от уведомлений).</summary>
    [HttpDelete("users/me/push-subscription")]
    public async Task<IActionResult> Unregister([FromBody] UnregisterPushRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        await _push.DeleteSubscriptionAsync(userId, request.Endpoint, ct);
        return NoContent();
    }

    /// <summary>Сгенерировать VAPID ключи (только для Admin).</summary>
    [HttpPost("admin/vapid/generate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateVapidKeys(CancellationToken ct)
    {
        var publicKey = await _push.GenerateVapidKeysAsync(ct);
        return Ok(new { publicKey, message = "VAPID ключи успешно сгенерированы" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

/// <summary>Запрос удаления push-подписки.</summary>
public sealed record UnregisterPushRequest(string Endpoint);
