using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Auth.Interfaces;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Infrastructure.Persistence.Repositories;

/// <summary>Реализация репозитория пользователей через EF Core.</summary>
public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<OrgUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.OrgUsers.FindAsync(new object[] { id }, ct);

    /// <inheritdoc />
    public async Task<AuthAccount?> GetAccountByUsernameAsync(string username, CancellationToken ct = default)
        => await _context.AuthAccounts
            .Include(a => a.User)
            .Include(a => a.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(a => a.Username == username, ct);

    /// <inheritdoc />
    public async Task<AuthSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await _context.AuthSessions
            .Include(s => s.Account)
                .ThenInclude(a => a.User)
            .Include(s => s.Account)
                .ThenInclude(a => a.UserRoles)
                    .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == tokenHash, ct);

    /// <inheritdoc />
    public Task AddSessionAsync(AuthSession session, CancellationToken ct = default)
    {
        _context.AuthSessions.Add(session);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
