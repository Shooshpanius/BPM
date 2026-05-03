using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Tasks.DTOs;
using CoreBPM.Server.Application.Tasks.Interfaces;
using CoreBPM.Server.Domain.Tasks;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Tasks.Services;

/// <summary>Реализация сервиса SLA-правил задач (FR-TASK-01.2).</summary>
public class TaskSlaService : ITaskSlaService
{
    private readonly AppDbContext _db;

    public TaskSlaService(AppDbContext db) => _db = db;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskSlaRuleDto>> ListAsync(CancellationToken ct = default)
        => await _db.TaskSlaRules.AsNoTracking()
            .OrderBy(r => r.CategoryId).ThenBy(r => r.Priority)
            .Select(r => ToDto(r))
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task<TaskSlaRuleDto> CreateAsync(UpsertTaskSlaRuleRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var now = DateTimeOffset.UtcNow;
        var rule = new TaskSlaRule
        {
            Id = Guid.NewGuid(),
            CategoryId = req.CategoryId?.Trim(),
            Priority = string.IsNullOrEmpty(req.Priority) ? null : Enum.Parse<TaskPriority>(req.Priority),
            DefaultDueHours = req.DefaultDueHours,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.TaskSlaRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    /// <inheritdoc/>
    public async Task<TaskSlaRuleDto> UpdateAsync(Guid id, UpsertTaskSlaRuleRequest req, CancellationToken ct = default)
    {
        Validate(req);
        var rule = await _db.TaskSlaRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException($"SLA-правило {id} не найдено.");
        rule.CategoryId = req.CategoryId?.Trim();
        rule.Priority = string.IsNullOrEmpty(req.Priority) ? null : Enum.Parse<TaskPriority>(req.Priority);
        rule.DefaultDueHours = req.DefaultDueHours;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _db.TaskSlaRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule == null) return;
        _db.TaskSlaRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> ComputeDueDateAsync(
        string? categoryId,
        TaskPriority priority,
        DateTimeOffset from,
        CancellationToken ct = default)
    {
        // Приоритет совпадения: (категория+приоритет) → (null категория+приоритет) → (категория+null приоритет) → (null+null)
        var rules = await _db.TaskSlaRules.AsNoTracking().ToListAsync(ct);
        var rule = rules.FirstOrDefault(r => r.CategoryId == categoryId && r.Priority == priority)
            ?? rules.FirstOrDefault(r => r.CategoryId == null && r.Priority == priority)
            ?? rules.FirstOrDefault(r => r.CategoryId == categoryId && r.Priority == null)
            ?? rules.FirstOrDefault(r => r.CategoryId == null && r.Priority == null);
        if (rule == null) return null;
        // Упрощённый расчёт: прибавляем часы без учёта рабочего графика.
        // TODO: при появлении конфигурации рабочих графиков заменить на расчёт по рабочему времени.
        return from.AddHours(rule.DefaultDueHours);
    }

    private static void Validate(UpsertTaskSlaRuleRequest req)
    {
        if (req.DefaultDueHours <= 0)
            throw new ValidationException("Срок по умолчанию в часах должен быть больше 0.");
    }

    private static TaskSlaRuleDto ToDto(TaskSlaRule r) => new()
    {
        Id = r.Id,
        CategoryId = r.CategoryId,
        Priority = r.Priority?.ToString(),
        DefaultDueHours = r.DefaultDueHours,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
    };
}
