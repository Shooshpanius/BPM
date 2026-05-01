using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления ролями (Владелец/Куратор) бизнес-процесса.</summary>
public class BpmProcessRoleService : IBpmProcessRoleService
{
    private readonly AppDbContext _db;

    public BpmProcessRoleService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessRoleConfigDto>> GetRolesAsync(Guid processId, CancellationToken ct = default)
    {
        var roles = await _db.BpmProcessRoleConfigs
            .AsNoTracking()
            .Where(r => r.ProcessId == processId)
            .OrderBy(r => r.RoleType)
            .ThenBy(r => r.SortOrder)
            .ToListAsync(ct);

        return roles.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessRoleConfigDto>> ReplaceRolesAsync(
        Guid processId,
        UpsertProcessRoleConfigsRequest request,
        CancellationToken ct = default)
    {
        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        // Удаляем все существующие записи ролей
        var existing = await _db.BpmProcessRoleConfigs
            .Where(r => r.ProcessId == processId)
            .ToListAsync(ct);
        _db.BpmProcessRoleConfigs.RemoveRange(existing);

        // Записываем новые
        var newRoles = request.Items.Select((item, idx) => new BpmProcessRoleConfig
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            RoleType = item.RoleType,
            AssigneeType = item.AssigneeType,
            AssigneeId = item.AssigneeId.Trim(),
            DisplayName = item.DisplayName.Trim(),
            SortOrder = item.SortOrder != 0 ? item.SortOrder : idx,
        }).ToList();

        _db.BpmProcessRoleConfigs.AddRange(newRoles);
        await _db.SaveChangesAsync(ct);

        return newRoles
            .OrderBy(r => r.RoleType)
            .ThenBy(r => r.SortOrder)
            .Select(MapToDto)
            .ToList();
    }

    private static BpmProcessRoleConfigDto MapToDto(BpmProcessRoleConfig r) =>
        new(r.Id, r.RoleType, r.AssigneeType, r.AssigneeId, r.DisplayName, r.SortOrder);
}
