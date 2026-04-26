using System.Security.Claims;
using CoreBPM.Server.Domain.Auth;

namespace CoreBPM.Server.Application.Auth.Interfaces;

/// <summary>Сервис для работы с JWT и refresh-токенами.</summary>
public interface ITokenService
{
    /// <summary>Генерирует JWT access token для аккаунта с указанными ролями.</summary>
    string GenerateAccessToken(AuthAccount account, IEnumerable<string> roles);

    /// <summary>Генерирует криптографически случайный refresh token (base64url, 64 байта).</summary>
    string GenerateRefreshToken();

    /// <summary>
    /// Валидирует access token без проверки времени жизни (для refresh-сценария).
    /// Возвращает null если токен некорректен.
    /// </summary>
    ClaimsPrincipal? ValidateAccessToken(string token);
}
