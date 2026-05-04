using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Org.DTOs;
using CoreBPM.Server.Application.Org.Interfaces;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Org.Services;

/// <summary>Реализация сервиса профиля пользователя (FR-ORG-02.1).</summary>
public class UserProfileService : IUserProfileService
{
    private readonly AppDbContext _db;

    public UserProfileService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        var user = await _db.OrgUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return null;

        // Загружаем активное назначение для получения должности/подразделения/организации
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignment = await _db.OrgPositionAssignments
            .AsNoTracking()
            .Include(a => a.Position)
            .Include(a => a.Department)
            .Include(a => a.Organization)
            .Where(a => a.UserId == userId &&
                        a.StartDate <= today &&
                        (a.EndDate == null || a.EndDate >= today))
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.StartDate)
            .FirstOrDefaultAsync(ct);

        var dto = MapToDto(user, assignment);

        // Скрываем дату рождения в соответствии с настройкой видимости
        if (user.BirthDateVisibility == "admin" && !isAdmin)
            dto.BirthDate = null;
        else if (user.BirthDateVisibility == "department")
        {
            // Определяем, находится ли актор в том же подразделении
            var inSameDept = assignment is not null &&
                await _db.OrgPositionAssignments
                    .AnyAsync(a => a.UserId == actorId &&
                                   (a.DepartmentId == assignment.DepartmentId ||
                                    a.Position.DepartmentId == assignment.DepartmentId) &&
                                   a.StartDate <= today &&
                                   (a.EndDate == null || a.EndDate >= today), ct);
            if (!inSameDept && !isAdmin)
                dto.BirthDate = null;
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<UserProfileDto> UpdateProfileAsync(Guid userId, Guid actorId, UpdateProfileRequest req, bool isAdmin, CancellationToken ct = default)
    {
        // Обычный пользователь может редактировать только свой профиль
        if (!isAdmin && userId != actorId)
            throw new UnauthorizedAccessException("Редактирование чужого профиля запрещено.");

        var user = await _db.OrgUsers.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"Пользователь {userId} не найден.");

        if (!string.IsNullOrWhiteSpace(req.FirstName)) user.FirstName = req.FirstName.Trim();
        if (!string.IsNullOrWhiteSpace(req.LastName)) user.LastName = req.LastName.Trim();
        if (req.MiddleName is not null) user.MiddleName = req.MiddleName.Trim();
        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.DisplayName = req.DisplayName.Trim();
        if (req.Phone is not null) user.Phone = req.Phone.Trim();
        if (req.MobilePhone is not null) user.MobilePhone = req.MobilePhone.Trim();
        if (req.InternalPhone is not null) user.InternalPhone = req.InternalPhone.Trim();
        if (req.PersonalEmail is not null) user.PersonalEmail = req.PersonalEmail.Trim();
        if (req.Bio is not null) user.Bio = req.Bio.Trim();
        if (req.BirthDate.HasValue) user.BirthDate = req.BirthDate;
        if (req.BirthDateVisibility is not null) user.BirthDateVisibility = req.BirthDateVisibility;

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return (await GetProfileAsync(userId, actorId, isAdmin, ct))!;
    }

    /// <inheritdoc />
    public async Task<string> SetAvatarUrlAsync(Guid userId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin && userId != actorId)
            throw new UnauthorizedAccessException("Изменение аватара чужого профиля запрещено.");

        var user = await _db.OrgUsers.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"Пользователь {userId} не найден.");

        // Заглушка: сохраняем плейсхолдер URL (реальное хранилище файлов не реализовано)
        var avatarUrl = $"/avatars/{userId}.jpg";
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return avatarUrl;
    }

    /// <inheritdoc />
    public async Task DeleteAvatarAsync(Guid userId, Guid actorId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin && userId != actorId)
            throw new UnauthorizedAccessException("Удаление аватара чужого профиля запрещено.");

        var user = await _db.OrgUsers.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException($"Пользователь {userId} не найден.");

        user.AvatarUrl = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Вспомогательные методы ─────────────────────────────────────────────────

    private static UserProfileDto MapToDto(
        Domain.Org.OrgUser user,
        Domain.Org.OrgPositionAssignment? assignment)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            MiddleName = user.MiddleName,
            DisplayName = user.DisplayName,
            WorkEmail = user.WorkEmail,
            Phone = user.Phone,
            MobilePhone = user.MobilePhone,
            InternalPhone = user.InternalPhone,
            PersonalEmail = user.PersonalEmail,
            Bio = user.Bio,
            BirthDate = user.BirthDate,
            BirthDateVisibility = user.BirthDateVisibility,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            Position = assignment?.Position?.Name,
            Department = assignment?.Department?.Name ?? assignment?.Position?.Department?.Name,
            Organization = assignment?.Organization?.Name
        };
    }
}
