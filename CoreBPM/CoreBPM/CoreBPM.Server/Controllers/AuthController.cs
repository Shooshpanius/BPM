using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoreBPM.Server.Application.Auth.DTOs;
using CoreBPM.Server.Application.Auth.Interfaces;
using CoreBPM.Server.Exceptions;

namespace CoreBPM.Server.Controllers;

/// <summary>Контроллер аутентификации: вход, обновление и отзыв токенов.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private const string RefreshTokenCookieName = "refresh_token";

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Вход в систему. Возвращает access token; refresh token устанавливается в HttpOnly cookie.</summary>
    /// <param name="request">Данные для входа.</param>
    /// <param name="ct">Токен отмены.</param>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var deviceInfo = Request.Headers.UserAgent.ToString();

        var response = await _authService.LoginAsync(request, ipAddress, deviceInfo, ct);

        // Устанавливаем refresh token в HttpOnly cookie
        SetRefreshTokenCookie(response.RefreshToken);

        return Ok(response);
    }

    /// <summary>Обновление access token по refresh token из HttpOnly cookie.</summary>
    /// <param name="ct">Токен отмены.</param>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RefreshResponse>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            throw new ForbiddenException("Токен обновления не найден");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _authService.RefreshAsync(refreshToken, ipAddress, ct);

        // Обновляем cookie с новым refresh token
        SetRefreshTokenCookie(response.RefreshToken);

        return Ok(response);
    }

    /// <summary>Выход из системы. Отзывает refresh token и удаляет cookie.</summary>
    /// <param name="ct">Токен отмены.</param>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await _authService.LogoutAsync(refreshToken, ct);

        // Удаляем cookie
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = true
        });

        return NoContent();
    }

    /// <summary>Устанавливает refresh token в защищённую HttpOnly cookie.</summary>
    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = true,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
