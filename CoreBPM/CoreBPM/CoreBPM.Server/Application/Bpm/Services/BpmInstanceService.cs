using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Exceptions;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Реализация сервиса управления экземплярами бизнес-процессов.</summary>
public class BpmInstanceService : IBpmInstanceService
{
    private readonly AppDbContext _db;
    private readonly IBpmExecutionEngine _engine;

    public BpmInstanceService(AppDbContext db, IBpmExecutionEngine engine)
    {
        _db = db;
        _engine = engine;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmInstanceListItemDto>> GetInstancesAsync(
        Guid processId,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var skip = (Math.Max(1, page) - 1) * Math.Min(100, pageSize);

        var instances = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => i.ProcessId == processId)
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .OrderByDescending(i => i.StartedAt)
            .Skip(skip)
            .Take(Math.Min(100, pageSize))
            .ToListAsync(ct);

        var userIds = instances
            .SelectMany(i => new[] { i.InitiatorUserId, i.ResponsibleUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var userNames = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetDisplayName(Guid? userId)
        {
            if (userId == null) return null;
            return userNames.FirstOrDefault(u => u.Id == userId)?.DisplayName;
        }

        return instances.Select(i => new BpmInstanceListItemDto(
            i.Id,
            i.ProcessId,
            i.Process.Name,
            i.ProcessVersionId,
            i.ProcessVersion.VersionNumber,
            i.Name,
            i.State,
            i.LaunchSource,
            i.InitiatorUserId,
            GetDisplayName(i.InitiatorUserId),
            i.ResponsibleUserId,
            GetDisplayName(i.ResponsibleUserId),
            i.StartedAt,
            i.CompletedAt,
            i.CancelledAt
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .AsNoTracking()
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        return await MapToDtoAsync(instance, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> CreateInstanceAsync(
        Guid processId,
        CreateInstanceRequest request,
        Guid initiatorUserId,
        CancellationToken ct = default)
    {
        var process = await _db.BpmProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        if (!process.LaunchFromPortalEnabled)
            throw new ForbiddenException("Запуск процесса из портала отключён в настройках");

        // Ищем активную версию
        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == processId && v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("Процесс не имеет активной опубликованной версии");

        var name = ResolveInstanceName(process, request);

        var now = DateTimeOffset.UtcNow;
        var instance = new BpmInstance
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            ProcessVersionId = version.Id,
            Name = name,
            State = BpmInstanceState.Active,
            LaunchSource = BpmInstanceLaunchSource.Manual,
            InitiatorUserId = initiatorUserId,
            ResponsibleUserId = initiatorUserId,
            ExternalReference = request.ExternalReference,
            StartedAt = now,
            UpdatedAt = now,
        };

        // Начальные переменные
        var variables = BuildVariables(instance.Id, request.Variables, now);
        instance.Variables = variables;

        _db.BpmInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        // Запускаем движок выполнения (fire-and-forget)
        _ = _engine.StartAsync(instance.Id, CancellationToken.None);

        return await GetInstanceByIdAsync(instance.Id, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> CreateInstanceViaWebhookAsync(
        string webhookKey,
        WebhookLaunchRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(webhookKey))
            throw new ValidationException("Ключ вебхука не может быть пустым");

        var tokenHash = ComputeSha256Hash(webhookKey);

        var process = await _db.BpmProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ExternalStartTokenHash == tokenHash && p.ExternalStartEnabled, ct)
            ?? throw new NotFoundException("Процесс с указанным ключом вебхука не найден или внешний запуск отключён");

        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == process.Id && v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("Процесс не имеет активной опубликованной версии");

        var now = DateTimeOffset.UtcNow;
        var instance = new BpmInstance
        {
            Id = Guid.NewGuid(),
            ProcessId = process.Id,
            ProcessVersionId = version.Id,
            Name = string.IsNullOrWhiteSpace(process.InstanceNameTemplate)
                ? $"{process.Name} — {now:dd.MM.yyyy HH:mm}"
                : process.InstanceNameTemplate,
            State = BpmInstanceState.Active,
            LaunchSource = BpmInstanceLaunchSource.Webhook,
            InitiatorUserId = null,
            ResponsibleUserId = null,
            ExternalReference = request.ExternalReference,
            StartedAt = now,
            UpdatedAt = now,
        };

        var webhookVariables = BuildVariables(instance.Id, request.Variables, now);
        instance.Variables = webhookVariables;

        _db.BpmInstances.Add(instance);
        await _db.SaveChangesAsync(ct);

        // Запускаем движок выполнения (fire-and-forget с CancellationToken.None:
        // запрос уже завершён, но движок должен продолжить работу)
        _ = _engine.StartAsync(instance.Id, CancellationToken.None);

        return await GetInstanceByIdAsync(instance.Id, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmSchedulerJobDto>> GetSchedulerJobsAsync(Guid processId, CancellationToken ct = default)
    {
        var jobs = await _db.BpmSchedulerJobs
            .AsNoTracking()
            .Where(j => j.ProcessId == processId)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);

        return jobs.Select(j => new BpmSchedulerJobDto(
            j.Id,
            j.ProcessId,
            j.ProcessVersionId,
            j.ElementId,
            j.TimerType,
            j.TimerValue,
            j.TimeZone,
            j.IsActive,
            j.LastFiredAt,
            j.NextFireAt,
            j.CreatedAt,
            j.UpdatedAt
        )).ToList();
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private static string ResolveInstanceName(BpmProcess process, CreateInstanceRequest request)
    {
        return process.InstanceNameMode switch
        {
            BpmInstanceNameMode.Template when !string.IsNullOrWhiteSpace(process.InstanceNameTemplate)
                => process.InstanceNameTemplate,
            BpmInstanceNameMode.Manual when !string.IsNullOrWhiteSpace(request.Name)
                => request.Name!.Trim(),
            _ => $"{process.Name} — {DateTimeOffset.UtcNow:dd.MM.yyyy HH:mm}",
        };
    }

    private static List<BpmInstanceVariable> BuildVariables(
        Guid instanceId,
        IDictionary<string, string?>? variables,
        DateTimeOffset now)
    {
        if (variables == null || variables.Count == 0)
            return new List<BpmInstanceVariable>();

        return variables.Select(kv => new BpmInstanceVariable
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            ProcessVariableId = null,
            Name = kv.Key.Trim(),
            ValueJson = kv.Value,
            SetAt = now,
        }).ToList();
    }

    private async Task<BpmInstanceDto> MapToDtoAsync(BpmInstance i, CancellationToken ct)
    {
        var userIds = new[] { i.InitiatorUserId, i.ResponsibleUserId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var userNames = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetDisplayName(Guid? userId)
        {
            if (userId == null) return null;
            return userNames.FirstOrDefault(u => u.Id == userId)?.DisplayName;
        }

        return new BpmInstanceDto(
            i.Id,
            i.ProcessId,
            i.Process.Name,
            i.ProcessVersionId,
            i.ProcessVersion.VersionNumber,
            i.Name,
            i.State,
            i.LaunchSource,
            i.InitiatorUserId,
            GetDisplayName(i.InitiatorUserId),
            i.ResponsibleUserId,
            GetDisplayName(i.ResponsibleUserId),
            i.ParentInstanceId,
            i.ExternalReference,
            i.CancelReason,
            i.StartedAt,
            i.CompletedAt,
            i.CancelledAt,
            i.UpdatedAt,
            i.Variables.Select(v => new BpmInstanceVariableDto(v.Id, v.Name, v.ValueJson)).ToList()
        );
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ─── Управление состоянием ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<BpmInstanceDto> CancelInstanceAsync(
        Guid instanceId,
        CancelInstanceRequest request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Причина прерывания обязательна");

        var instance = await _db.BpmInstances
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        if (instance.State == BpmInstanceState.Completed || instance.State == BpmInstanceState.Cancelled)
            throw new ValidationException($"Нельзя прервать экземпляр в состоянии {instance.State}");

        var now = DateTimeOffset.UtcNow;
        instance.State = BpmInstanceState.Cancelled;
        instance.CancelReason = request.Reason.Trim();
        instance.CancelledAt = now;
        instance.UpdatedAt = now;

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.Cancelled,
            ActorUserId = actorUserId,
            Text = request.Reason.Trim(),
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return await MapToDtoAsync(instance, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> SuspendInstanceAsync(
        Guid instanceId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        if (instance.State != BpmInstanceState.Active)
            throw new ValidationException("Приостановить можно только активный экземпляр");

        var now = DateTimeOffset.UtcNow;
        instance.State = BpmInstanceState.Suspended;
        instance.UpdatedAt = now;

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.Suspended,
            ActorUserId = actorUserId,
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return await MapToDtoAsync(instance, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> ResumeInstanceAsync(
        Guid instanceId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        if (instance.State != BpmInstanceState.Suspended)
            throw new ValidationException("Возобновить можно только приостановленный экземпляр");

        var now = DateTimeOffset.UtcNow;
        instance.State = BpmInstanceState.Active;
        instance.UpdatedAt = now;

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.Resumed,
            ActorUserId = actorUserId,
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return await MapToDtoAsync(instance, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> ChangeResponsibleAsync(
        Guid instanceId,
        ChangeResponsibleRequest request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        if (instance.State == BpmInstanceState.Cancelled || instance.State == BpmInstanceState.Completed)
            throw new ValidationException("Нельзя изменить ответственного у завершённого или прерванного экземпляра");

        var oldResponsible = instance.ResponsibleUserId;
        var now = DateTimeOffset.UtcNow;

        var newUser = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.Id == request.NewResponsibleUserId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Пользователь {request.NewResponsibleUserId} не найден");

        instance.ResponsibleUserId = request.NewResponsibleUserId;
        instance.UpdatedAt = now;

        var meta = System.Text.Json.JsonSerializer.Serialize(new
        {
            from = oldResponsible,
            to = request.NewResponsibleUserId,
            toName = newUser.DisplayName,
        });

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.ResponsibleChanged,
            ActorUserId = actorUserId,
            Text = $"Ответственный изменён на «{newUser.DisplayName}»",
            MetaJson = meta,
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return await MapToDtoAsync(instance, ct);
    }

    /// <inheritdoc />
    public async Task<BpmInstanceVariableDto> UpdateVariableAsync(
        Guid instanceId,
        string variableName,
        UpdateInstanceVariableRequest request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        var variable = await _db.BpmInstanceVariables
            .FirstOrDefaultAsync(v => v.InstanceId == instanceId && v.Name == variableName, ct);

        var now = DateTimeOffset.UtcNow;
        var oldValue = variable?.ValueJson;

        if (variable == null)
        {
            variable = new BpmInstanceVariable
            {
                Id = Guid.NewGuid(),
                InstanceId = instanceId,
                Name = variableName.Trim(),
                ValueJson = request.ValueJson,
                SetAt = now,
            };
            _db.BpmInstanceVariables.Add(variable);
        }
        else
        {
            variable.ValueJson = request.ValueJson;
            variable.SetAt = now;
        }

        var meta = System.Text.Json.JsonSerializer.Serialize(new { name = variableName, from = oldValue, to = request.ValueJson });
        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.VariableUpdated,
            ActorUserId = actorUserId,
            Text = $"Переменная «{variableName}» изменена",
            MetaJson = meta,
            OccurredAt = now,
        });

        instance.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new BpmInstanceVariableDto(variable.Id, variable.Name, variable.ValueJson);
    }

    // ─── История ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmInstanceHistoryEntryDto>> GetHistoryAsync(
        Guid instanceId,
        CancellationToken ct = default)
    {
        var entries = await _db.BpmInstanceHistoryEntries
            .AsNoTracking()
            .Where(e => e.InstanceId == instanceId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);

        var actorIds = entries
            .Where(e => e.ActorUserId.HasValue)
            .Select(e => e.ActorUserId!.Value)
            .Distinct().ToList();

        var actorNames = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        return entries.Select(e => new BpmInstanceHistoryEntryDto(
            e.Id,
            e.EventType,
            e.ActorUserId,
            actorNames.FirstOrDefault(u => u.Id == e.ActorUserId)?.DisplayName,
            e.Text,
            e.MetaJson,
            e.OccurredAt
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmInstanceHistoryEntryDto> AddCommentAsync(
        Guid instanceId,
        AddCommentRequest request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new ValidationException("Текст комментария не может быть пустым");

        var exists = await _db.BpmInstances.AnyAsync(i => i.Id == instanceId, ct);
        if (!exists) throw new NotFoundException($"Экземпляр {instanceId} не найден");

        var now = DateTimeOffset.UtcNow;
        var entry = new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = request.IsQuestion ? BpmHistoryEventType.QuestionAdded : BpmHistoryEventType.CommentAdded,
            ActorUserId = actorUserId,
            Text = request.Text.Trim(),
            OccurredAt = now,
        };

        _db.BpmInstanceHistoryEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        var actor = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct);

        return new BpmInstanceHistoryEntryDto(
            entry.Id, entry.EventType, entry.ActorUserId,
            actor?.DisplayName, entry.Text, entry.MetaJson, entry.OccurredAt);
    }

    // ─── Участники ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmInstanceParticipantDto>> GetParticipantsAsync(
        Guid instanceId,
        CancellationToken ct = default)
    {
        var participants = await _db.BpmInstanceParticipants
            .AsNoTracking()
            .Where(p => p.InstanceId == instanceId)
            .OrderBy(p => p.AddedAt)
            .ToListAsync(ct);

        var userIds = participants
            .SelectMany(p => new[] { (Guid?)p.UserId, p.AddedByUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetName(Guid? id) => userNames.FirstOrDefault(u => u.Id == id)?.DisplayName;

        return participants.Select(p => new BpmInstanceParticipantDto(
            p.Id, p.UserId, GetName(p.UserId), p.AddedByUserId, GetName(p.AddedByUserId), p.AddedAt
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<BpmInstanceParticipantDto> AddParticipantAsync(
        Guid instanceId,
        AddParticipantRequest request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var exists = await _db.BpmInstances.AnyAsync(i => i.Id == instanceId, ct);
        if (!exists) throw new NotFoundException($"Экземпляр {instanceId} не найден");

        var already = await _db.BpmInstanceParticipants
            .AnyAsync(p => p.InstanceId == instanceId && p.UserId == request.UserId, ct);
        if (already) throw new ValidationException("Пользователь уже является участником");

        var user = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.Id == request.UserId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Пользователь {request.UserId} не найден");

        var now = DateTimeOffset.UtcNow;
        var participant = new BpmInstanceParticipant
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            UserId = request.UserId,
            DisplayName = user.DisplayName,
            AddedByUserId = actorUserId,
            AddedAt = now,
        };

        _db.BpmInstanceParticipants.Add(participant);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.ParticipantAdded,
            ActorUserId = actorUserId,
            Text = $"Добавлен участник «{user.DisplayName}»",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);

        var addedBy = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.Id == actorUserId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct);

        return new BpmInstanceParticipantDto(
            participant.Id, participant.UserId, user.DisplayName, actorUserId, addedBy?.DisplayName, participant.AddedAt);
    }

    /// <inheritdoc />
    public async Task RemoveParticipantAsync(
        Guid instanceId,
        Guid participantUserId,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var participant = await _db.BpmInstanceParticipants
            .FirstOrDefaultAsync(p => p.InstanceId == instanceId && p.UserId == participantUserId, ct)
            ?? throw new NotFoundException("Участник не найден");

        var user = await _db.OrgUsers.AsNoTracking()
            .Where(u => u.Id == participantUserId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct);

        _db.BpmInstanceParticipants.Remove(participant);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.ParticipantRemoved,
            ActorUserId = actorUserId,
            Text = $"Удалён участник «{user?.DisplayName ?? participantUserId.ToString()}»",
            OccurredAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync(ct);
    }

    // ─── Мои процессы ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<MyInstancesResult> GetMyInstancesAsync(
        Guid userId,
        MyInstancesFilter filter,
        int page = 1,
        int pageSize = 30,
        CancellationToken ct = default)
    {
        // Идентификаторы экземпляров, где пользователь — участник
        var participantInstanceIds = filter.Role is MyInstancesRole.All or MyInstancesRole.Participant
            ? await _db.BpmInstanceParticipants
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => p.InstanceId)
                .ToListAsync(ct)
            : [];

        var query = _db.BpmInstances
            .AsNoTracking()
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .AsQueryable();

        // Фильтр по роли
        query = filter.Role switch
        {
            MyInstancesRole.Initiator => query.Where(i => i.InitiatorUserId == userId),
            MyInstancesRole.Responsible => query.Where(i => i.ResponsibleUserId == userId),
            MyInstancesRole.Participant => query.Where(i => participantInstanceIds.Contains(i.Id)),
            _ => query.Where(i =>
                i.InitiatorUserId == userId ||
                i.ResponsibleUserId == userId ||
                participantInstanceIds.Contains(i.Id))
        };

        // Фильтр по состоянию
        if (filter.State.HasValue)
            query = query.Where(i => i.State == filter.State.Value);

        // Быстрый поиск по названию
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchLower = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(searchLower));
        }

        // Фильтр по процессу
        if (filter.ProcessId.HasValue)
            query = query.Where(i => i.ProcessId == filter.ProcessId.Value);

        // Фильтр по дате запуска
        if (filter.DateFrom.HasValue)
            query = query.Where(i => i.StartedAt >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue)
            query = query.Where(i => i.StartedAt <= filter.DateTo.Value);

        var total = await query.CountAsync(ct);

        var skip = (Math.Max(1, page) - 1) * Math.Min(100, pageSize);
        var instances = await query
            .OrderByDescending(i => i.StartedAt)
            .Skip(skip)
            .Take(Math.Min(100, pageSize))
            .ToListAsync(ct);

        var userIds = instances
            .SelectMany(i => new[] { i.InitiatorUserId, i.ResponsibleUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var userNames = await _db.OrgUsers
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? Name(Guid? id) => userNames.FirstOrDefault(u => u.Id == id)?.DisplayName;

        var items = instances.Select(i => new BpmInstanceListItemDto(
            i.Id, i.ProcessId, i.Process.Name, i.ProcessVersionId, i.ProcessVersion.VersionNumber,
            i.Name, i.State, i.LaunchSource,
            i.InitiatorUserId, Name(i.InitiatorUserId),
            i.ResponsibleUserId, Name(i.ResponsibleUserId),
            i.StartedAt, i.CompletedAt, i.CancelledAt
        )).ToList();

        return new MyInstancesResult(items, total);
    }

    /// <inheritdoc />
    public async Task<byte[]> ExportMyInstancesToCsvAsync(
        Guid userId,
        MyInstancesFilter filter,
        CancellationToken ct = default)
    {
        // Повторяем ту же логику фильтрации, но без пагинации (до 5000 строк)
        var participantInstanceIds = filter.Role is MyInstancesRole.All or MyInstancesRole.Participant
            ? await _db.BpmInstanceParticipants
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .Select(p => p.InstanceId)
                .ToListAsync(ct)
            : new List<Guid>();

        var query = _db.BpmInstances
            .AsNoTracking()
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .AsQueryable();

        query = filter.Role switch
        {
            MyInstancesRole.Initiator  => query.Where(i => i.InitiatorUserId == userId),
            MyInstancesRole.Responsible => query.Where(i => i.ResponsibleUserId == userId),
            MyInstancesRole.Participant => query.Where(i => participantInstanceIds.Contains(i.Id)),
            _ => query.Where(i =>
                i.InitiatorUserId == userId ||
                i.ResponsibleUserId == userId ||
                participantInstanceIds.Contains(i.Id))
        };

        if (filter.State.HasValue) query = query.Where(i => i.State == filter.State.Value);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLower();
            query = query.Where(i => i.Name.ToLower().Contains(s));
        }
        if (filter.ProcessId.HasValue) query = query.Where(i => i.ProcessId == filter.ProcessId.Value);
        if (filter.DateFrom.HasValue) query = query.Where(i => i.StartedAt >= filter.DateFrom.Value);
        if (filter.DateTo.HasValue) query = query.Where(i => i.StartedAt <= filter.DateTo.Value);

        var instances = await query
            .OrderByDescending(i => i.StartedAt)
            .Take(5000)
            .ToListAsync(ct);

        var userIds = instances
            .SelectMany(i => new[] { i.InitiatorUserId, i.ResponsibleUserId })
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var userNames = await _db.OrgUsers.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        string? GetName(Guid? id) => userNames.FirstOrDefault(u => u.Id == id)?.DisplayName;

        return BuildCsv(
            ["Экземпляр", "Процесс", "Версия", "Состояние", "Инициатор", "Ответственный", "Запущен", "Завершён"],
            instances.Select(i => new[]
            {
                i.Name,
                i.Process.Name,
                i.ProcessVersion.VersionNumber.ToString(),
                i.State.ToString(),
                GetName(i.InitiatorUserId) ?? "",
                GetName(i.ResponsibleUserId) ?? "",
                i.StartedAt.ToString("dd.MM.yyyy HH:mm"),
                (i.CompletedAt ?? i.CancelledAt)?.ToString("dd.MM.yyyy HH:mm") ?? "",
            })
        );
    }

    /// <inheritdoc />
    public async Task<BatchLaunchResult> BatchCreateInstancesAsync(
        Guid processId,
        BatchLaunchRequest request,
        Guid initiatorUserId,
        CancellationToken ct = default)
    {
        if (request.Items == null || request.Items.Count == 0)
            throw new ValidationException("Список элементов для запуска не может быть пустым");
        if (request.Items.Count > 500)
            throw new ValidationException("Пакетный запуск поддерживает не более 500 экземпляров за раз");

        var process = await _db.BpmProcesses
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == processId, ct)
            ?? throw new NotFoundException($"Процесс {processId} не найден");

        if (!process.LaunchFromPortalEnabled)
            throw new ForbiddenException("Запуск процесса из портала отключён в настройках");

        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == processId && v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("Процесс не имеет активной опубликованной версии");

        var now = DateTimeOffset.UtcNow;
        var results = new List<BatchLaunchItemResult>();

        for (var idx = 0; idx < request.Items.Count; idx++)
        {
            var item = request.Items[idx];
            try
            {
                var name = ResolveInstanceName(process, new CreateInstanceRequest(item.Name, item.Variables));
                var instance = new BpmInstance
                {
                    Id = Guid.NewGuid(),
                    ProcessId = processId,
                    ProcessVersionId = version.Id,
                    Name = name,
                    State = BpmInstanceState.Active,
                    LaunchSource = BpmInstanceLaunchSource.Batch,
                    InitiatorUserId = initiatorUserId,
                    ResponsibleUserId = initiatorUserId,
                    StartedAt = now.AddMilliseconds(idx),
                    UpdatedAt = now.AddMilliseconds(idx),
                };
                instance.Variables = BuildVariables(instance.Id, item.Variables, now.AddMilliseconds(idx));
                _db.BpmInstances.Add(instance);

                results.Add(new BatchLaunchItemResult(true, instance.Id, instance.Name, null));
            }
            catch (Exception ex)
            {
                results.Add(new BatchLaunchItemResult(false, null, null, ex.Message));
            }
        }

        await _db.SaveChangesAsync(ct);

        // Запускаем движок для каждого успешно созданного экземпляра (fire-and-forget)
        foreach (var r in results.Where(r => r.Success && r.InstanceId.HasValue))
            _ = _engine.StartAsync(r.InstanceId!.Value, CancellationToken.None);

        return new BatchLaunchResult(
            Total: results.Count,
            Created: results.Count(r => r.Success),
            Failed: results.Count(r => !r.Success),
            Items: results
        );
    }

    /// <inheritdoc />
    public async Task<BpmInstanceDto> SwitchVersionAsync(
        Guid instanceId,
        SwitchInstanceVersionRequest request,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var instance = await _db.BpmInstances
            .Include(i => i.Process)
            .Include(i => i.ProcessVersion)
            .Include(i => i.Variables)
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new NotFoundException($"Экземпляр {instanceId} не найден");

        if (instance.State == BpmInstanceState.Cancelled || instance.State == BpmInstanceState.Completed)
            throw new ValidationException("Нельзя переключить версию у завершённого или прерванного экземпляра");

        if (instance.ProcessVersionId == request.TargetVersionId)
            throw new ValidationException("Экземпляр уже выполняется на указанной версии");

        var targetVersion = await _db.BpmProcessVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v =>
                v.Id == request.TargetVersionId &&
                v.ProcessId == instance.ProcessId &&
                v.Status == BpmProcessVersionStatus.Active, ct)
            ?? throw new NotFoundException("Целевая версия не найдена или не является активной версией данного процесса");

        var oldVersionNumber = instance.ProcessVersion.VersionNumber;
        var now = DateTimeOffset.UtcNow;

        instance.ProcessVersionId = targetVersion.Id;
        instance.UpdatedAt = now;

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.VariableUpdated,
            ActorUserId = actorUserId,
            Text = $"Версия процесса изменена с v{oldVersionNumber} на v{targetVersion.VersionNumber}",
            MetaJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = "VersionSwitch",
                fromVersionId = instance.ProcessVersionId,
                toVersionId = targetVersion.Id,
                toVersionNumber = targetVersion.VersionNumber,
            }),
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
        return await GetInstanceByIdAsync(instanceId, ct);
    }

    // ─── Вспомогательные методы ──────────────────────────────────────────────

    /// <summary>Формирует CSV-байты (UTF-8 BOM) из заголовка и строк.</summary>
    private static byte[] BuildCsv(IReadOnlyList<string> headers, IEnumerable<string[]> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(";", headers.Select(EscapeCsv)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(";", row.Select(EscapeCsv)));

        var content = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        // Добавляем UTF-8 BOM для корректного открытия в Excel
        return [0xEF, 0xBB, 0xBF, .. content];
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
