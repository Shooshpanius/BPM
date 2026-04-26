using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Org;

namespace CoreBPM.Server.Infrastructure.Persistence;

/// <summary>
/// Инициализатор администратора: при первом запуске создаёт пользователя admin
/// с паролем из конфигурации (Admin:Password / env BPM_S_Admin__Password) и ролью Admin.
/// </summary>
public static class AdminSeeder
{
    private const string AdminRoleName = "Admin";
    private const int BcryptWorkFactor = 12;

    /// <summary>
    /// Создаёт пользователя admin, если он отсутствует в базе данных.
    /// Безопасно запускать при каждом старте — повторного создания не происходит.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Admin:Password не задан (env BPM_S_Admin__Password). " +
                "Пользователь admin не будет создан автоматически.");
            return;
        }

        // Проверяем, существует ли уже аккаунт admin
        var exists = await db.AuthAccounts.AnyAsync(a => a.Username == "admin");
        if (exists)
        {
            logger.LogDebug("Пользователь admin уже существует — пропускаем сидинг.");
            return;
        }

        // Получаем роль Admin из базы данных по имени
        var adminRole = await db.AuthRoles.FirstOrDefaultAsync(r => r.Name == AdminRoleName);
        if (adminRole is null)
        {
            logger.LogError(
                "Роль '{RoleName}' не найдена в базе данных. " +
                "Убедитесь, что миграции применены корректно.",
                AdminRoleName);
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // 1. Профиль пользователя в org_users
        var orgUser = new OrgUser
        {
            Id = Guid.NewGuid(),
            FirstName = "Admin",
            LastName = "System",
            DisplayName = "Системный администратор",
            WorkEmail = "admin@local",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        // 2. Учётная запись в auth_accounts
        var account = new AuthAccount
        {
            Id = Guid.NewGuid(),
            UserId = orgUser.Id,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: BcryptWorkFactor),
            IsLocked = false,
            FailedLoginCount = 0,
            LastPasswordChangeAt = now,
            MustChangePassword = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        // 3. Назначение роли Admin
        var userRole = new AuthUserRole
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            RoleId = adminRole.Id,
            AssignedAt = now
        };

        db.OrgUsers.Add(orgUser);
        db.AuthAccounts.Add(account);
        db.AuthUserRoles.Add(userRole);

        await db.SaveChangesAsync();

        logger.LogInformation("Пользователь admin успешно создан с ролью Admin.");
    }
}
