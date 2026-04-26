using System.Security.Cryptography;
using System.Text;
using CoreBPM.Server.Application.Auth.DTOs;
using CoreBPM.Server.Application.Auth.Interfaces;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Exceptions;

namespace CoreBPM.Server.Application.Auth.Services;

/// <summary>Сервис аутентификации: вход, обновление токена, выход.</summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? deviceInfo,
        CancellationToken ct = default)
    {
        // 1. Получить аккаунт по username
        var account = await _userRepository.GetAccountByUsernameAsync(request.Username, ct);

        // 2. Если не найден или пользователь неактивен — скрытая ошибка
        if (account is null || !account.User.IsActive)
            throw new NotFoundException("Неверный логин или пароль");

        // 3. Проверить блокировку аккаунта
        if (account.IsLocked && (account.LockedUntil is null || account.LockedUntil > DateTimeOffset.UtcNow))
            throw new ForbiddenException("Аккаунт заблокирован");

        // Снять блокировку если срок истёк
        if (account.IsLocked && account.LockedUntil <= DateTimeOffset.UtcNow)
        {
            account.IsLocked = false;
            account.FailedLoginCount = 0;
        }

        // 4. Проверить пароль
        if (!BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
        {
            account.FailedLoginCount++;

            var maxAttempts = _configuration.GetValue<int>("Auth:MaxFailedAttempts", 5);
            var lockoutMinutes = _configuration.GetValue<int>("Auth:LockoutMinutes", 15);

            if (account.FailedLoginCount >= maxAttempts)
            {
                account.IsLocked = true;
                account.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(lockoutMinutes);
            }

            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _userRepository.SaveChangesAsync(ct);
            throw new NotFoundException("Неверный логин или пароль");
        }

        // 5. Сбросить счётчик ошибок
        account.FailedLoginCount = 0;
        account.IsLocked = false;
        account.UpdatedAt = DateTimeOffset.UtcNow;

        // 6. Получить роли и сгенерировать токены
        var roles = account.UserRoles.Select(ur => ur.Role.Name).ToList();
        var accessToken = _tokenService.GenerateAccessToken(account, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // 7. Сохранить сессию с SHA256-хешем refresh token
        var refreshDays = _configuration.GetValue<int>("Jwt:RefreshTokenLifetimeDays", 7);
        var session = new AuthSession
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            RefreshTokenHash = HashToken(refreshToken),
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(refreshDays),
            LastUsedAt = DateTimeOffset.UtcNow,
            IsRevoked = false
        };

        await _userRepository.AddSessionAsync(session, ct);
        await _userRepository.SaveChangesAsync(ct);

        var lifetimeMinutes = _configuration.GetValue<int>("Jwt:AccessTokenLifetimeMinutes", 30);
        return new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresIn = lifetimeMinutes * 60,
            RefreshToken = refreshToken
        };
    }

    /// <inheritdoc />
    public async Task<RefreshResponse> RefreshAsync(
        string refreshToken,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // 1. Хешировать входящий refresh token
        var tokenHash = HashToken(refreshToken);

        // 2. Найти сессию по хешу
        var session = await _userRepository.GetSessionByTokenHashAsync(tokenHash, ct);

        // 3. Проверить валидность сессии
        if (session is null || session.IsRevoked || session.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ForbiddenException("Недействительный токен обновления");

        var account = session.Account;

        // 4. Получить роли
        var roles = account.UserRoles.Select(ur => ur.Role.Name).ToList();

        // 5. Выпустить новые токены
        var newAccessToken = _tokenService.GenerateAccessToken(account, roles);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        // 6. Обновить сессию
        session.RefreshTokenHash = HashToken(newRefreshToken);
        session.LastUsedAt = DateTimeOffset.UtcNow;
        if (ipAddress is not null)
            session.IpAddress = ipAddress;

        await _userRepository.SaveChangesAsync(ct);

        var lifetimeMinutes = _configuration.GetValue<int>("Jwt:AccessTokenLifetimeMinutes", 30);
        return new RefreshResponse
        {
            AccessToken = newAccessToken,
            ExpiresIn = lifetimeMinutes * 60,
            RefreshToken = newRefreshToken
        };
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        // 1. Хешировать refresh token
        var tokenHash = HashToken(refreshToken);

        // 2. Найти и инвалидировать сессию
        var session = await _userRepository.GetSessionByTokenHashAsync(tokenHash, ct);
        if (session is not null)
        {
            session.IsRevoked = true;
            await _userRepository.SaveChangesAsync(ct);
        }
    }

    /// <summary>Вычисляет SHA256-хеш токена в виде hex-строки.</summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
