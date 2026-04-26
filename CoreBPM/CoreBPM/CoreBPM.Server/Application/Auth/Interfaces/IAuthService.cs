using CoreBPM.Server.Application.Auth.DTOs;

namespace CoreBPM.Server.Application.Auth.Interfaces;

/// <summary>Сервис аутентификации пользователей.</summary>
public interface IAuthService
{
    /// <summary>Выполняет вход пользователя и создаёт сессию.</summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default);

    /// <summary>Обновляет access token по refresh token.</summary>
    Task<RefreshResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken ct = default);

    /// <summary>Завершает сессию пользователя, инвалидируя refresh token.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
