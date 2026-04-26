using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Application.Auth.Interfaces;

/// <summary>Репозиторий для работы с пользователями и сессиями.</summary>
public interface IUserRepository
{
    /// <summary>Получает профиль пользователя по идентификатору.</summary>
    Task<OrgUser?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Получает аккаунт по имени пользователя (логину).</summary>
    Task<AuthAccount?> GetAccountByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Получает сессию по SHA256-хешу refresh token.</summary>
    Task<AuthSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Добавляет новую сессию.</summary>
    Task AddSessionAsync(AuthSession session, CancellationToken ct = default);

    /// <summary>Сохраняет изменения в базе данных.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
