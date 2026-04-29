using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления переменными контекста бизнес-процесса.</summary>
public class BpmVariableService : IBpmVariableService
{
    private readonly AppDbContext _db;

    public BpmVariableService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmProcessVariableDto>> GetVariablesAsync(Guid processId, CancellationToken ct = default)
    {
        var variables = await _db.BpmProcessVariables
            .AsNoTracking()
            .Where(v => v.ProcessId == processId)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .ToListAsync(ct);

        return variables.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmProcessVariableDto> CreateVariableAsync(Guid processId, CreateBpmVariableRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        var processExists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!processExists)
            throw new NotFoundException($"Процесс {processId} не найден");

        var duplicate = await _db.BpmProcessVariables
            .AnyAsync(v => v.ProcessId == processId && v.Name == request.Name.Trim(), ct);
        if (duplicate)
            throw new ValidationException($"Переменная с именем «{request.Name}» уже существует в этом процессе");

        // Новая переменная добавляется в конец
        var maxOrder = await _db.BpmProcessVariables
            .Where(v => v.ProcessId == processId)
            .Select(v => (int?)v.SortOrder)
            .MaxAsync(ct) ?? -1;

        var variable = new BpmProcessVariable
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            Name = request.Name.Trim(),
            VariableType = request.VariableType,
            DefaultValue = request.DefaultValue?.Trim(),
            IsKeyVariable = request.IsKeyVariable,
            IsInput = request.IsInput,
            IsOutput = request.IsOutput,
            SortOrder = maxOrder + 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        // Ключевой может быть только одна переменная
        if (request.IsKeyVariable)
            await ClearKeyVariableFlagAsync(processId, null, ct);

        _db.BpmProcessVariables.Add(variable);
        await _db.SaveChangesAsync(ct);
        return MapToDto(variable);
    }

    /// <inheritdoc />
    public async Task<BpmProcessVariableDto> UpdateVariableAsync(Guid processId, Guid variableId, UpdateBpmVariableRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);

        var variable = await _db.BpmProcessVariables
            .FirstOrDefaultAsync(v => v.Id == variableId && v.ProcessId == processId, ct)
            ?? throw new NotFoundException($"Переменная {variableId} не найдена");

        var duplicate = await _db.BpmProcessVariables
            .AnyAsync(v => v.ProcessId == processId && v.Name == request.Name.Trim() && v.Id != variableId, ct);
        if (duplicate)
            throw new ValidationException($"Переменная с именем «{request.Name}» уже существует в этом процессе");

        if (request.IsKeyVariable)
            await ClearKeyVariableFlagAsync(processId, variableId, ct);

        variable.Name = request.Name.Trim();
        variable.VariableType = request.VariableType;
        variable.DefaultValue = request.DefaultValue?.Trim();
        variable.IsKeyVariable = request.IsKeyVariable;
        variable.IsInput = request.IsInput;
        variable.IsOutput = request.IsOutput;
        variable.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(variable);
    }

    /// <inheritdoc />
    public async Task DeleteVariableAsync(Guid processId, Guid variableId, CancellationToken ct = default)
    {
        var variable = await _db.BpmProcessVariables
            .FirstOrDefaultAsync(v => v.Id == variableId && v.ProcessId == processId, ct)
            ?? throw new NotFoundException($"Переменная {variableId} не найдена");

        _db.BpmProcessVariables.Remove(variable);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReorderVariablesAsync(Guid processId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        var variables = await _db.BpmProcessVariables
            .Where(v => v.ProcessId == processId)
            .ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var variable = variables.FirstOrDefault(v => v.Id == orderedIds[i]);
            if (variable is not null)
            {
                variable.SortOrder = i;
                variable.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── Вспомогательные методы ─────────────────────────────────────────────

    private async Task ClearKeyVariableFlagAsync(Guid processId, Guid? excludeId, CancellationToken ct)
    {
        var existing = await _db.BpmProcessVariables
            .Where(v => v.ProcessId == processId && v.IsKeyVariable && (excludeId == null || v.Id != excludeId))
            .ToListAsync(ct);

        foreach (var v in existing)
        {
            v.IsKeyVariable = false;
            v.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Имя переменной обязательно");
        if (name.Length > 200)
            throw new ValidationException("Имя переменной не должно превышать 200 символов");
    }

    private static BpmProcessVariableDto MapToDto(BpmProcessVariable v) =>
        new(v.Id, v.Name, v.VariableType, v.DefaultValue, v.IsKeyVariable, v.IsInput, v.IsOutput, v.SortOrder);
}
