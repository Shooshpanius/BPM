using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Admin.DTOs;
using CoreBPM.Server.Application.Admin.Interfaces;
using CoreBPM.Server.Application.Tasks.Interfaces;
using CoreBPM.Server.Domain.Auth;
using CoreBPM.Server.Domain.Org;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Admin.Services;

/// <summary>Сервис управления пользователями в административной панели.</summary>
public class AdminUserService : IAdminUserService
{
    private readonly AppDbContext _db;
    private readonly ITaskService _taskService;
    private const int BcryptWorkFactor = 12;

    public AdminUserService(AppDbContext db, ITaskService taskService)
    {
        _db = db;
        _taskService = taskService;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminUserListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.OrgUsers
            .AsNoTracking()
            .Include(u => u.Account)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new AdminUserListItemDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                MiddleName = u.MiddleName,
                DisplayName = u.DisplayName,
                WorkEmail = u.WorkEmail,
                Phone = u.Phone,
                IsActive = u.IsActive,
                Username = u.Account != null ? u.Account.Username : null,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.OrgUsers
            .AsNoTracking()
            .Include(u => u.Account)
            .Where(u => u.Id == id)
            .Select(u => new AdminUserListItemDto
            {
                Id = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                MiddleName = u.MiddleName,
                DisplayName = u.DisplayName,
                WorkEmail = u.WorkEmail,
                Phone = u.Phone,
                IsActive = u.IsActive,
                Username = u.Account != null ? u.Account.Username : null,
                CreatedAt = u.CreatedAt
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Пользователь {id} не найден");

        return user;
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        ValidateCreateRequest(request);

        // Проверка уникальности email и username
        if (await _db.OrgUsers.AnyAsync(u => u.WorkEmail == request.WorkEmail, ct))
            throw new ValidationException($"Пользователь с email '{request.WorkEmail}' уже существует");

        if (await _db.AuthAccounts.AnyAsync(a => a.Username == request.Username, ct))
            throw new ValidationException($"Пользователь с логином '{request.Username}' уже существует");

        var now = DateTimeOffset.UtcNow;
        var displayName = BuildDisplayName(request.LastName, request.FirstName, request.MiddleName);

        var orgUser = new OrgUser
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            MiddleName = request.MiddleName?.Trim(),
            DisplayName = displayName,
            WorkEmail = request.WorkEmail.Trim().ToLowerInvariant(),
            Phone = request.Phone?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        var account = new AuthAccount
        {
            Id = Guid.NewGuid(),
            UserId = orgUser.Id,
            Username = request.Username.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: BcryptWorkFactor),
            IsLocked = false,
            FailedLoginCount = 0,
            LastPasswordChangeAt = now,
            MustChangePassword = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.OrgUsers.Add(orgUser);
        _db.AuthAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return MapToDto(orgUser, account.Username);
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ValidationException("Имя пользователя обязательно");
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException("Фамилия пользователя обязательна");
        if (string.IsNullOrWhiteSpace(request.WorkEmail))
            throw new ValidationException("Email пользователя обязателен");

        var user = await _db.OrgUsers
            .Include(u => u.Account)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException($"Пользователь {id} не найден");

        // Проверяем уникальность email, исключая текущего пользователя
        if (await _db.OrgUsers.AnyAsync(u => u.WorkEmail == request.WorkEmail.Trim().ToLowerInvariant() && u.Id != id, ct))
            throw new ValidationException($"Email '{request.WorkEmail}' уже используется другим пользователем");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.MiddleName = request.MiddleName?.Trim();
        user.DisplayName = BuildDisplayName(request.LastName, request.FirstName, request.MiddleName);
        user.WorkEmail = request.WorkEmail.Trim().ToLowerInvariant();
        user.Phone = request.Phone?.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToDto(user, user.Account?.Username);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.OrgUsers.FindAsync(new object[] { id }, ct)
            ?? throw new NotFoundException($"Пользователь {id} не найден");

        // Мягкое удаление — деактивируем пользователя
        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // FR-TASK-01.5.2: перенаправить открытые ProcessTask-задачи заблокированного пользователя
        await _taskService.ReassignBlockedProcessTasksAsync(id, ct);
    }

    private static void ValidateCreateRequest(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            throw new ValidationException("Имя пользователя обязательно");
        if (string.IsNullOrWhiteSpace(request.LastName))
            throw new ValidationException("Фамилия пользователя обязательна");
        if (string.IsNullOrWhiteSpace(request.WorkEmail))
            throw new ValidationException("Email пользователя обязателен");
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ValidationException("Логин пользователя обязателен");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ValidationException("Пароль должен содержать не менее 8 символов");
    }

    private static string BuildDisplayName(string lastName, string firstName, string? middleName)
    {
        var parts = new[] { lastName.Trim(), firstName.Trim(), middleName?.Trim() }
            .Where(p => !string.IsNullOrEmpty(p));
        return string.Join(" ", parts);
    }

    private static AdminUserListItemDto MapToDto(OrgUser user, string? username) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        MiddleName = user.MiddleName,
        DisplayName = user.DisplayName,
        WorkEmail = user.WorkEmail,
        Phone = user.Phone,
        IsActive = user.IsActive,
        Username = username,
        CreatedAt = user.CreatedAt
    };
}
