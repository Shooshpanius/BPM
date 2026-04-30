using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления пользовательскими статусами экземпляров бизнес-процесса.</summary>
public class BpmInstanceStatusService : IBpmInstanceStatusService
{
    private readonly AppDbContext _db;

    public BpmInstanceStatusService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<InstanceStatusConfigDto> GetConfigAsync(Guid processId, CancellationToken ct = default)
    {
        await EnsureProcessExistsAsync(processId, ct);

        var config = await _db.BpmInstanceStatusConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProcessId == processId, ct);

        var options = await _db.BpmInstanceStatusOptions
            .AsNoTracking()
            .Where(o => o.ProcessId == processId)
            .OrderBy(o => o.SortOrder)
            .ThenBy(o => o.Name)
            .ToListAsync(ct);

        string? linkedVariableName = null;
        if (config?.LinkedVariableId is not null)
        {
            linkedVariableName = await _db.BpmProcessVariables
                .AsNoTracking()
                .Where(v => v.Id == config.LinkedVariableId)
                .Select(v => v.Name)
                .FirstOrDefaultAsync(ct);
        }

        return MapToConfigDto(config, linkedVariableName, options);
    }

    /// <inheritdoc />
    public async Task<InstanceStatusConfigDto> UpdateConfigAsync(Guid processId, UpdateStatusConfigRequest request, CancellationToken ct = default)
    {
        await EnsureProcessExistsAsync(processId, ct);

        var config = await _db.BpmInstanceStatusConfigs
            .FirstOrDefaultAsync(c => c.ProcessId == processId, ct);

        if (config is null)
        {
            config = new BpmInstanceStatusConfig
            {
                Id = Guid.NewGuid(),
                ProcessId = processId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _db.BpmInstanceStatusConfigs.Add(config);
        }

        Guid? linkedVariableId = request.LinkedVariableId;

        // Создать новую переменную типа List и прилинковать
        if (request.CreateVariable)
        {
            if (string.IsNullOrWhiteSpace(request.NewVariableName))
                throw new ValidationException("Имя новой переменной обязательно");

            var varName = request.NewVariableName.Trim();

            var duplicate = await _db.BpmProcessVariables
                .AnyAsync(v => v.ProcessId == processId && v.Name == varName, ct);
            if (duplicate)
                throw new ValidationException($"Переменная с именем «{varName}» уже существует в этом процессе");

            var maxOrder = await _db.BpmProcessVariables
                .Where(v => v.ProcessId == processId)
                .Select(v => (int?)v.SortOrder)
                .MaxAsync(ct) ?? -1;

            var newVar = new BpmProcessVariable
            {
                Id = Guid.NewGuid(),
                ProcessId = processId,
                Name = varName,
                VariableType = BpmVariableType.List,
                IsKeyVariable = false,
                IsInput = false,
                IsOutput = false,
                SortOrder = maxOrder + 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _db.BpmProcessVariables.Add(newVar);
            linkedVariableId = newVar.Id;
        }
        else if (linkedVariableId is not null)
        {
            // Проверяем, что переменная принадлежит процессу
            var varExists = await _db.BpmProcessVariables
                .AnyAsync(v => v.Id == linkedVariableId && v.ProcessId == processId, ct);
            if (!varExists)
                throw new NotFoundException($"Переменная {linkedVariableId} не найдена в процессе {processId}");
        }

        config.LinkedVariableId = linkedVariableId;
        config.OnInterruptAction = request.OnInterruptAction;
        config.OnInterruptScriptId = request.OnInterruptScriptId?.Trim();
        config.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        return await GetConfigAsync(processId, ct);
    }

    /// <inheritdoc />
    public async Task<InstanceStatusOptionDto> CreateOptionAsync(Guid processId, CreateStatusOptionRequest request, CancellationToken ct = default)
    {
        await EnsureProcessExistsAsync(processId, ct);

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название статуса обязательно");
        if (request.Name.Length > 300)
            throw new ValidationException("Название статуса не должно превышать 300 символов");

        var code = string.IsNullOrWhiteSpace(request.Code)
            ? GenerateCode(request.Name)
            : request.Code.Trim();

        ValidateCode(code);
        await EnsureCodeUniqueAsync(processId, code, excludeId: null, ct);

        var maxOrder = await _db.BpmInstanceStatusOptions
            .Where(o => o.ProcessId == processId)
            .Select(o => (int?)o.SortOrder)
            .MaxAsync(ct) ?? -1;

        var option = new BpmInstanceStatusOption
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            Name = request.Name.Trim(),
            Code = code,
            SortOrder = maxOrder + 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.BpmInstanceStatusOptions.Add(option);
        await _db.SaveChangesAsync(ct);
        return MapToOptionDto(option);
    }

    /// <inheritdoc />
    public async Task<InstanceStatusOptionDto> UpdateOptionAsync(Guid processId, Guid optionId, UpdateStatusOptionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Название статуса обязательно");
        if (request.Name.Length > 300)
            throw new ValidationException("Название статуса не должно превышать 300 символов");

        var code = request.Code.Trim();
        ValidateCode(code);

        var option = await _db.BpmInstanceStatusOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProcessId == processId, ct)
            ?? throw new NotFoundException($"Статус {optionId} не найден");

        await EnsureCodeUniqueAsync(processId, code, excludeId: optionId, ct);

        option.Name = request.Name.Trim();
        option.Code = code;
        option.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToOptionDto(option);
    }

    /// <inheritdoc />
    public async Task DeleteOptionAsync(Guid processId, Guid optionId, CancellationToken ct = default)
    {
        var option = await _db.BpmInstanceStatusOptions
            .FirstOrDefaultAsync(o => o.Id == optionId && o.ProcessId == processId, ct)
            ?? throw new NotFoundException($"Статус {optionId} не найден");

        _db.BpmInstanceStatusOptions.Remove(option);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ReorderOptionsAsync(Guid processId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        var options = await _db.BpmInstanceStatusOptions
            .Where(o => o.ProcessId == processId)
            .ToListAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var option = options.FirstOrDefault(o => o.Id == orderedIds[i]);
            if (option is not null)
            {
                option.SortOrder = i;
                option.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ─── Вспомогательные методы ─────────────────────────────────────────────

    private async Task EnsureProcessExistsAsync(Guid processId, CancellationToken ct)
    {
        var exists = await _db.BpmProcesses.AnyAsync(p => p.Id == processId, ct);
        if (!exists)
            throw new NotFoundException($"Процесс {processId} не найден");
    }

    private async Task EnsureCodeUniqueAsync(Guid processId, string code, Guid? excludeId, CancellationToken ct)
    {
        var duplicate = await _db.BpmInstanceStatusOptions
            .AnyAsync(o => o.ProcessId == processId && o.Code == code && (excludeId == null || o.Id != excludeId), ct);
        if (duplicate)
            throw new ValidationException($"Статус с кодом «{code}» уже существует в этом процессе");
    }

    /// <summary>Генерирует код из названия: транслитерация → kebab-case.</summary>
    private static string GenerateCode(string name)
    {
        var transliterated = Transliterate(name);
        var code = Regex.Replace(transliterated.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(code) ? "status" : code;
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("Код статуса обязателен");
        if (code.Length > 200)
            throw new ValidationException("Код статуса не должен превышать 200 символов");
        if (!Regex.IsMatch(code, @"^[a-zA-Z0-9_\-]+$"))
            throw new ValidationException("Код статуса должен содержать только латинские буквы, цифры, дефис или нижнее подчёркивание");
    }

    private static string Transliterate(string value)
    {
        var map = new Dictionary<char, string>
        {
            ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh", ['з'] = "z",
            ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
            ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch",
            ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
        };

        var sb = new StringBuilder();
        foreach (var ch in value.ToLowerInvariant())
        {
            if (map.TryGetValue(ch, out var mapped))
                sb.Append(mapped);
            else if (ch <= 127)
                sb.Append(ch);
            else if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    private static InstanceStatusConfigDto MapToConfigDto(
        BpmInstanceStatusConfig? config,
        string? linkedVariableName,
        IReadOnlyList<BpmInstanceStatusOption> options)
    {
        return new InstanceStatusConfigDto(
            LinkedVariableId: config?.LinkedVariableId,
            LinkedVariableName: linkedVariableName,
            OnInterruptAction: config?.OnInterruptAction ?? BpmInterruptAction.KeepCurrent,
            OnInterruptScriptId: config?.OnInterruptScriptId,
            Options: options.Select(MapToOptionDto).ToList()
        );
    }

    private static InstanceStatusOptionDto MapToOptionDto(BpmInstanceStatusOption o) =>
        new(o.Id, o.Name, o.Code, o.SortOrder);
}
