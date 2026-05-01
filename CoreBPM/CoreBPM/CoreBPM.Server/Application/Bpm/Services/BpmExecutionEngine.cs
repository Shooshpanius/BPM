using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Application.Bpm.Scripting;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Infrastructure.Persistence;

namespace CoreBPM.Server.Application.Bpm.Services;

/// <summary>Движок выполнения BPMN-процессов.</summary>
public class BpmExecutionEngine : IBpmExecutionEngine
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";

    /// <summary>Максимальная длина сообщения об ошибке, сохраняемого в БД.</summary>
    private const int MaxErrorLength = 4000;

    /// <summary>Таймаут выполнения ScriptTask по умолчанию (мс).</summary>
    private const int ScriptTimeoutMs = 30_000;

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBpmScriptExecutor _scriptExecutor;
    private readonly ILogger<BpmExecutionEngine> _logger;

    public BpmExecutionEngine(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IBpmScriptExecutor scriptExecutor,
        ILogger<BpmExecutionEngine> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _scriptExecutor = scriptExecutor;
        _logger = logger;
    }

    // ─── Публичный API ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task StartAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await LoadInstanceAsync(instanceId, ct);
        if (instance == null)
        {
            _logger.LogWarning("StartAsync: экземпляр {InstanceId} не найден", instanceId);
            return;
        }

        var model = await ParseModelAsync(instance.ProcessVersionId, ct);
        if (model == null) return;

        var now = DateTimeOffset.UtcNow;

        // Добавляем запись «Запуск» в историю
        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.Started,
            OccurredAt = now,
        });
        await _db.SaveChangesAsync(ct);

        // Продвигаем каждое стартовое событие
        foreach (var startEvent in model.StartEvents)
        {
            await ExecuteElementAsync(instance, model, startEvent.Id, startEvent.ElementType, startEvent.Name, now, ct);
        }
    }

    /// <inheritdoc />
    public async Task AdvanceFromAsync(Guid instanceId, string fromElementId, CancellationToken ct = default)
    {
        var instance = await LoadInstanceAsync(instanceId, ct);
        if (instance == null) return;

        if (instance.State == BpmInstanceState.Cancelled ||
            instance.State == BpmInstanceState.Completed ||
            instance.State == BpmInstanceState.Suspended)
        {
            _logger.LogDebug("AdvanceFromAsync пропущен: экземпляр {InstanceId} в состоянии {State}", instanceId, instance.State.ToString());
            return;
        }

        var model = await ParseModelAsync(instance.ProcessVersionId, ct);
        if (model == null) return;

        var now = DateTimeOffset.UtcNow;

        // Завершаем токен текущего элемента
        await CompleteTokenAsync(instanceId, fromElementId, now, ct);

        // Загружаем переменные для вычисления условий
        var variables = await _db.BpmInstanceVariables
            .AsNoTracking()
            .Where(v => v.InstanceId == instanceId)
            .ToDictionaryAsync(v => v.Name, v => v.ValueJson, ct);

        // Определяем исходящие потоки
        var outgoingFlows = model.SequenceFlows
            .Where(f => f.SourceRef == fromElementId)
            .ToList();

        if (outgoingFlows.Count == 0)
        {
            _logger.LogDebug("Нет исходящих потоков от {ElementId} в экземпляре {InstanceId}", fromElementId, instanceId);
            return;
        }

        // Получаем тип текущего элемента для логики шлюзов
        var currentElement = model.AllElements.FirstOrDefault(e => e.Id == fromElementId);
        var isExclusiveGateway = currentElement?.ElementType is "exclusiveGateway" or "eventBasedGateway";
        var isInclusiveGateway = currentElement?.ElementType is "inclusiveGateway" or "complexGateway";
        var isParallelGateway = currentElement?.ElementType is "parallelGateway";

        List<SequenceFlowInfo> chosenFlows;

        if (isExclusiveGateway)
        {
            // Exclusive: первый поток с выполненным условием (или поток по умолчанию)
            var conditionFlow = outgoingFlows
                .Where(f => !string.IsNullOrWhiteSpace(f.ConditionExpression))
                .FirstOrDefault(f => EvaluateCondition(f.ConditionExpression!, variables));

            if (conditionFlow != null)
            {
                chosenFlows = [conditionFlow];
            }
            else
            {
                // Поток по умолчанию или первый без условия
                var defaultFlow = outgoingFlows.FirstOrDefault(f => string.IsNullOrWhiteSpace(f.ConditionExpression));
                chosenFlows = defaultFlow != null ? [defaultFlow] : [outgoingFlows.First()];
            }
        }
        else if (isInclusiveGateway)
        {
            // Inclusive: все потоки с выполненным условием
            chosenFlows = outgoingFlows
                .Where(f => string.IsNullOrWhiteSpace(f.ConditionExpression) ||
                            EvaluateCondition(f.ConditionExpression!, variables))
                .ToList();

            if (chosenFlows.Count == 0)
                chosenFlows = [outgoingFlows.First()];
        }
        else if (isParallelGateway)
        {
            // Parallel: все исходящие потоки
            chosenFlows = outgoingFlows;
        }
        else
        {
            // Обычный элемент или шлюз с одним выходом: все потоки (обычно один)
            chosenFlows = outgoingFlows
                .Where(f => string.IsNullOrWhiteSpace(f.ConditionExpression) ||
                            EvaluateCondition(f.ConditionExpression!, variables))
                .ToList();
            if (chosenFlows.Count == 0) chosenFlows = [outgoingFlows.First()];
        }

        foreach (var flow in chosenFlows)
        {
            var target = model.AllElements.FirstOrDefault(e => e.Id == flow.TargetRef);
            if (target == null)
            {
                _logger.LogWarning("Целевой элемент {TargetRef} не найден в модели", flow.TargetRef);
                continue;
            }

            await ExecuteElementAsync(instance, model, target.Id, target.ElementType, target.Name, now, ct);
        }
    }

    /// <inheritdoc />
    public async Task<BpmTokenDto> CompleteUserTaskAsync(
        Guid instanceId,
        string elementId,
        Guid actorUserId,
        IDictionary<string, string?>? outputVariables,
        CancellationToken ct = default)
    {
        var token = await _db.BpmTokens
            .FirstOrDefaultAsync(t =>
                t.InstanceId == instanceId &&
                t.ElementId == elementId &&
                t.Status == BpmTokenStatus.WaitingUserAction, ct)
            ?? throw new Exceptions.NotFoundException($"Активный UserTask-токен для элемента {elementId} не найден");

        var now = DateTimeOffset.UtcNow;

        // Сохраняем выходные переменные (загружаем все за один запрос для избежания N+1)
        if (outputVariables != null && outputVariables.Count > 0)
        {
            var varNames = outputVariables.Keys.Select(k => k.Trim()).ToList();
            var existingVars = await _db.BpmInstanceVariables
                .Where(v => v.InstanceId == instanceId && varNames.Contains(v.Name))
                .ToDictionaryAsync(v => v.Name, ct);

            foreach (var kv in outputVariables)
            {
                var name = kv.Key.Trim();
                if (existingVars.TryGetValue(name, out var variable))
                {
                    variable.ValueJson = kv.Value;
                    variable.SetAt = now;
                }
                else
                {
                    _db.BpmInstanceVariables.Add(new BpmInstanceVariable
                    {
                        Id = Guid.NewGuid(),
                        InstanceId = instanceId,
                        Name = name,
                        ValueJson = kv.Value,
                        SetAt = now,
                    });
                }
            }
        }

        // Добавляем запись в историю
        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.NodeExecuted,
            ActorUserId = actorUserId,
            ElementId = elementId,
            ElementName = token.ElementName,
            DurationMs = (long)(now - token.CreatedAt).TotalMilliseconds,
            Text = $"Пользовательская задача «{token.ElementName ?? elementId}» выполнена",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);

        var result = MapTokenToDto(token);

        // Продвигаем поток вперёд
        await AdvanceFromAsync(instanceId, elementId, ct);

        return result;
    }

    /// <inheritdoc />
    public async Task SendSignalAsync(string signalCode, CancellationToken ct = default)
    {
        var waitingTokens = await _db.BpmTokens
            .Where(t => t.Status == BpmTokenStatus.WaitingSignal && t.SignalCode == signalCode)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in waitingTokens)
        {
            _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
            {
                Id = Guid.NewGuid(),
                InstanceId = token.InstanceId,
                EventType = BpmHistoryEventType.NodeExecuted,
                ElementId = token.ElementId,
                ElementName = token.ElementName,
                DurationMs = (long)(now - token.CreatedAt).TotalMilliseconds,
                Text = $"Сигнал «{signalCode}» получен элементом «{token.ElementName ?? token.ElementId}»",
                OccurredAt = now,
            });
            await _db.SaveChangesAsync(ct);
            await AdvanceFromAsync(token.InstanceId, token.ElementId, ct);
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string messageCode, string? correlationKey, CancellationToken ct = default)
    {
        var query = _db.BpmTokens
            .Where(t => t.Status == BpmTokenStatus.WaitingMessage && t.MessageCode == messageCode);

        if (!string.IsNullOrWhiteSpace(correlationKey))
            query = query.Where(t => t.CorrelationKey == correlationKey);

        var waitingTokens = await query.ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in waitingTokens)
        {
            _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
            {
                Id = Guid.NewGuid(),
                InstanceId = token.InstanceId,
                EventType = BpmHistoryEventType.NodeExecuted,
                ElementId = token.ElementId,
                ElementName = token.ElementName,
                DurationMs = (long)(now - token.CreatedAt).TotalMilliseconds,
                Text = $"Сообщение «{messageCode}» получено элементом «{token.ElementName ?? token.ElementId}»",
                OccurredAt = now,
            });
            await _db.SaveChangesAsync(ct);
            await AdvanceFromAsync(token.InstanceId, token.ElementId, ct);
        }
    }

    /// <inheritdoc />
    public async Task ExecuteJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.BpmExecutionJobs
            .Include(j => j.Instance)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job == null)
        {
            _logger.LogWarning("ExecuteJobAsync: задание {JobId} не найдено", jobId);
            return;
        }

        if (job.Status != BpmJobStatus.Running)
        {
            _logger.LogWarning("ExecuteJobAsync: задание {JobId} не в статусе Running (текущий: {Status})", jobId, job.Status);
            return;
        }

        var instance = job.Instance;
        if (instance == null)
        {
            _logger.LogWarning("ExecuteJobAsync: задание {JobId} не привязано к экземпляру", jobId);
            job.Status = BpmJobStatus.Failed;
            job.LastError = "Экземпляр процесса не найден";
            job.FailedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var configJson = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == job.ProcessId && c.ElementId == job.ElementId)
            .Select(c => c.ConfigJson)
            .FirstOrDefaultAsync(ct);

        var startedAt = job.StartedAt ?? DateTimeOffset.UtcNow;
        var now = DateTimeOffset.UtcNow;

        try
        {
            await DispatchJobAsync(job, instance, configJson, ct);

            // Успех
            job.Status = BpmJobStatus.Completed;
            job.CompletedAt = now;

            _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                EventType = BpmHistoryEventType.NodeExecuted,
                ElementId = job.ElementId,
                DurationMs = (long)(now - startedAt).TotalMilliseconds,
                Text = $"Задание «{job.OperationName ?? job.ElementType}» успешно выполнено",
                OccurredAt = now,
            });

            await _db.SaveChangesAsync(ct);

            // Продвигаем поток вперёд
            await AdvanceFromAsync(instance.Id, job.ElementId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения задания {JobId} ({ElementType} #{Attempt})", jobId, job.ElementType, job.AttemptNumber);

            job.LastError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;
            job.FailedAt = now;

            if (job.AttemptNumber >= job.MaxAttempts)
            {
                // Исчерпаны попытки — переводим экземпляр в Faulted
                job.Status = BpmJobStatus.Failed;

                _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    EventType = BpmHistoryEventType.NodeFailed,
                    ElementId = job.ElementId,
                    DurationMs = (long)(now - startedAt).TotalMilliseconds,
                    Text = $"Задание «{job.OperationName ?? job.ElementType}» завершилось ошибкой после {job.AttemptNumber} попыток: {ex.Message}",
                    OccurredAt = now,
                });

                instance.State = BpmInstanceState.Faulted;
                instance.UpdatedAt = now;
            }
            else
            {
                // Планируем повторную попытку с экспоненциальной задержкой
                var delaySeconds = Math.Min(300, (int)Math.Pow(2, job.AttemptNumber) * 10);
                job.Status = BpmJobStatus.Scheduled;
                job.NextRunAt = now.AddSeconds(delaySeconds);
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BpmTokenDto>> GetTokensAsync(Guid instanceId, CancellationToken ct = default)
    {
        var tokens = await _db.BpmTokens
            .AsNoTracking()
            .Where(t => t.InstanceId == instanceId && t.Status != BpmTokenStatus.Completed)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        return tokens.Select(MapTokenToDto).ToList();
    }

    // ─── Внутренняя логика выполнения узлов ───────────────────────────────────

    private async Task ExecuteElementAsync(
        BpmInstance instance,
        ProcessModel model,
        string elementId,
        string elementType,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        _logger.LogDebug("ExecuteElement: {ElementType} [{ElementId}] в экземпляре {InstanceId}", elementType, elementId, instance.Id);

        switch (elementType)
        {
            case "startEvent":
                // Стартовое событие — немедленно двигаемся дальше
                await CreateOrUpdateTokenAsync(instance.Id, elementId, elementType, elementName, BpmTokenStatus.Completed, now, ct);
                await AdvanceFromAsync(instance.Id, elementId, ct);
                break;

            case "endEvent":
            case "terminateEvent":
                await HandleEndEventAsync(instance, elementId, elementName, now, ct);
                break;

            case "userTask":
            case "receiveTask":
                await HandleUserTaskAsync(instance.Id, elementId, elementType, elementName, now, ct);
                break;

            case "serviceTask":
            case "scriptTask":
            case "sendTask":
            case "businessRuleTask":
                await HandleAsyncTaskAsync(instance, model, elementId, elementType, elementName, now, ct);
                break;

            case "exclusiveGateway":
            case "inclusiveGateway":
            case "parallelGateway":
            case "eventBasedGateway":
            case "complexGateway":
                // Шлюзы сами по себе не ждут — немедленно двигаемся дальше
                await CreateOrUpdateTokenAsync(instance.Id, elementId, elementType, elementName, BpmTokenStatus.Active, now, ct);
                await AdvanceFromAsync(instance.Id, elementId, ct);
                break;

            case "intermediateCatchEvent":
                await HandleIntermediateCatchEventAsync(instance.Id, model, elementId, elementName, now, ct);
                break;

            case "intermediateThrowEvent":
                await HandleIntermediateThrowEventAsync(instance.Id, model, elementId, elementName, now, ct);
                break;

            case "callActivity":
                // В MVP — логируем как выполненный узел и двигаемся дальше
                await CreateOrUpdateTokenAsync(instance.Id, elementId, elementType, elementName, BpmTokenStatus.Completed, now, ct);
                _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    EventType = BpmHistoryEventType.NodeExecuted,
                    ElementId = elementId,
                    ElementName = elementName,
                    Text = $"CallActivity «{elementName ?? elementId}» (заглушка, исполнение отложено)",
                    OccurredAt = now,
                });
                await _db.SaveChangesAsync(ct);
                await AdvanceFromAsync(instance.Id, elementId, ct);
                break;

            default:
                // Неизвестный тип — пассивный проход
                _logger.LogDebug("Неизвестный тип элемента {ElementType}, пассивный проход", elementType);
                await CreateOrUpdateTokenAsync(instance.Id, elementId, elementType, elementName, BpmTokenStatus.Completed, now, ct);
                await AdvanceFromAsync(instance.Id, elementId, ct);
                break;
        }
    }

    private async Task HandleEndEventAsync(
        BpmInstance instance,
        string elementId,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await CreateOrUpdateTokenAsync(instance.Id, elementId, "endEvent", elementName, BpmTokenStatus.Completed, now, ct);

        // Завершаем экземпляр (перезагружаем для отслеживания)
        var tracked = await _db.BpmInstances.FirstOrDefaultAsync(i => i.Id == instance.Id, ct);
        if (tracked != null && tracked.State == BpmInstanceState.Active)
        {
            tracked.State = BpmInstanceState.Completed;
            tracked.CompletedAt = now;
            tracked.UpdatedAt = now;
        }

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            EventType = BpmHistoryEventType.Completed,
            ElementId = elementId,
            ElementName = elementName,
            Text = "Процесс завершён",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleUserTaskAsync(
        Guid instanceId,
        string elementId,
        string elementType,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await CreateOrUpdateTokenAsync(instanceId, elementId, elementType, elementName, BpmTokenStatus.WaitingUserAction, now, ct);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = elementName,
            Text = $"Пользовательская задача «{elementName ?? elementId}» ожидает выполнения",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleAsyncTaskAsync(
        BpmInstance instance,
        ProcessModel model,
        string elementId,
        string elementType,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await CreateOrUpdateTokenAsync(instance.Id, elementId, elementType, elementName, BpmTokenStatus.Active, now, ct);

        // Загружаем политику ошибок из конфигурации элемента
        var configJson = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == instance.ProcessId && c.ElementId == elementId)
            .Select(c => c.ConfigJson)
            .FirstOrDefaultAsync(ct);

        var maxAttempts = 3;
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (doc.RootElement.TryGetProperty("maxRetries", out var mr) && mr.TryGetInt32(out var mrv))
                    maxAttempts = Math.Max(1, mrv + 1);
            }
            catch { /* игнорируем ошибки разбора */ }
        }

        var version = model.VersionId;
        var job = new BpmExecutionJob
        {
            Id = Guid.NewGuid(),
            ProcessId = instance.ProcessId,
            ProcessVersionId = version,
            InstanceId = instance.Id,
            ElementId = elementId,
            ElementType = elementType,
            OperationName = elementName,
            Status = BpmJobStatus.Pending,
            MaxAttempts = maxAttempts,
            NextRunAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.BpmExecutionJobs.Add(job);

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleIntermediateCatchEventAsync(
        Guid instanceId,
        ProcessModel model,
        string elementId,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var eventNode = model.AllElements.FirstOrDefault(e => e.Id == elementId);
        var signalCode = eventNode?.SignalCode;
        var messageCode = eventNode?.MessageCode;

        BpmTokenStatus status;
        if (!string.IsNullOrWhiteSpace(signalCode))
            status = BpmTokenStatus.WaitingSignal;
        else if (!string.IsNullOrWhiteSpace(messageCode))
            status = BpmTokenStatus.WaitingMessage;
        else
            status = BpmTokenStatus.WaitingUserAction; // TimerEvent — упрощённо ждём

        var token = new BpmToken
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            ElementId = elementId,
            ElementType = "intermediateCatchEvent",
            ElementName = elementName,
            Status = status,
            SignalCode = signalCode,
            MessageCode = messageCode,
            CreatedAt = now,
        };
        _db.BpmTokens.Add(token);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = elementName,
            Text = status switch
            {
                BpmTokenStatus.WaitingSignal => $"Ожидание сигнала «{signalCode}»",
                BpmTokenStatus.WaitingMessage => $"Ожидание сообщения «{messageCode}»",
                _ => $"Промежуточное событие «{elementName ?? elementId}» ожидает",
            },
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task HandleIntermediateThrowEventAsync(
        Guid instanceId,
        ProcessModel model,
        string elementId,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var eventNode = model.AllElements.FirstOrDefault(e => e.Id == elementId);
        var signalCode = eventNode?.SignalCode;

        await CreateOrUpdateTokenAsync(instanceId, elementId, "intermediateThrowEvent", elementName, BpmTokenStatus.Completed, now, ct);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = elementName,
            Text = !string.IsNullOrWhiteSpace(signalCode)
                ? $"Сигнал «{signalCode}» отправлен"
                : $"Промежуточное событие «{elementName ?? elementId}» сработало",
            OccurredAt = now,
        });
        await _db.SaveChangesAsync(ct);

        // Если это событие отправляет сигнал — рассылаем его другим экземплярам
        if (!string.IsNullOrWhiteSpace(signalCode))
            await SendSignalAsync(signalCode, ct);

        await AdvanceFromAsync(instanceId, elementId, ct);
    }

    // ─── Выполнение заданий (ServiceTask/ScriptTask) ──────────────────────────

    private async Task DispatchJobAsync(
        BpmExecutionJob job,
        BpmInstance instance,
        string? configJson,
        CancellationToken ct)
    {
        switch (job.ElementType)
        {
            case "serviceTask":
            case "sendTask":
                await ExecuteServiceTaskAsync(job, instance, configJson, ct);
                break;

            case "scriptTask":
                await ExecuteScriptTaskAsync(job, instance, configJson, ct);
                break;

            case "businessRuleTask":
                ExecuteBusinessRuleTaskStub(job);
                break;

            default:
                _logger.LogInformation("Задание типа {ElementType} — пассивное выполнение (заглушка)", job.ElementType);
                break;
        }
    }

    private async Task ExecuteServiceTaskAsync(
        BpmExecutionJob job,
        BpmInstance instance,
        string? configJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            _logger.LogDebug("ServiceTask {ElementId}: конфигурация отсутствует, пропуск", job.ElementId);
            return;
        }

        using var doc = JsonDocument.Parse(configJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("operationType", out var opTypeProp))
        {
            _logger.LogDebug("ServiceTask {ElementId}: operationType не указан", job.ElementId);
            return;
        }

        var operationType = opTypeProp.GetString();

        switch (operationType)
        {
            case "HttpCall":
                await ExecuteHttpCallAsync(job, instance, root, ct);
                break;

            case "ChangeInstanceStatus":
                await ExecuteChangeInstanceStatusAsync(instance, root, ct);
                break;

            default:
                _logger.LogInformation("ServiceTask operationType «{OperationType}» не реализован, пропуск", operationType);
                break;
        }
    }

    private async Task ExecuteHttpCallAsync(
        BpmExecutionJob job,
        BpmInstance instance,
        JsonElement config,
        CancellationToken ct)
    {
        var url = config.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        var method = config.TryGetProperty("method", out var methodProp) ? methodProp.GetString() ?? "GET" : "GET";

        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("HttpCall: URL не указан для элемента {ElementId}", job.ElementId);
            return;
        }

        // Подставляем переменные вида {{variableName}} в URL и тело
        var variables = await _db.BpmInstanceVariables
            .AsNoTracking()
            .Where(v => v.InstanceId == instance.Id)
            .ToDictionaryAsync(v => v.Name, v => v.ValueJson ?? "", ct);

        url = SubstituteVariables(url, variables);

        var httpClient = _httpClientFactory.CreateClient("BpmEngine");
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (config.TryGetProperty("body", out var bodyProp) && bodyProp.ValueKind != JsonValueKind.Null)
        {
            var bodyStr = bodyProp.GetString() ?? "";
            bodyStr = SubstituteVariables(bodyStr, variables);
            request.Content = new StringContent(bodyStr, System.Text.Encoding.UTF8, "application/json");
        }

        // Заголовки из конфигурации
        if (config.TryGetProperty("headers", out var headersProp) && headersProp.ValueKind == JsonValueKind.Object)
        {
            foreach (var header in headersProp.EnumerateObject())
            {
                try { request.Headers.TryAddWithoutValidation(header.Name, header.Value.GetString()); }
                catch { /* пропускаем невалидные заголовки */ }
            }
        }

        var response = await httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var truncatedBody = body.Length > 500 ? body[..500] : body;
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {truncatedBody}");
        }

        _logger.LogInformation("HttpCall: {Method} {Url} → {StatusCode}", method, url, (int)response.StatusCode);
    }

    private async Task ExecuteChangeInstanceStatusAsync(
        BpmInstance instance,
        JsonElement config,
        CancellationToken ct)
    {
        var statusCode = config.TryGetProperty("statusCode", out var sc) ? sc.GetString() : null;
        if (string.IsNullOrWhiteSpace(statusCode)) return;

        // Ищем конфигурацию статусов процесса
        var statusConfig = await _db.BpmInstanceStatusConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProcessId == instance.ProcessId, ct);

        if (statusConfig?.LinkedVariableId == null) return;

        // Определяем имя привязанной переменной
        var processVariable = await _db.BpmProcessVariables
            .AsNoTracking()
            .Where(v => v.Id == statusConfig.LinkedVariableId.Value)
            .Select(v => v.Name)
            .FirstOrDefaultAsync(ct);

        if (processVariable == null) return;

        var variable = await _db.BpmInstanceVariables
            .FirstOrDefaultAsync(v => v.InstanceId == instance.Id && v.Name == processVariable, ct);

        var now = DateTimeOffset.UtcNow;
        var jsonValue = $"\"{statusCode}\"";
        if (variable == null)
        {
            _db.BpmInstanceVariables.Add(new BpmInstanceVariable
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                Name = processVariable,
                ValueJson = jsonValue,
                SetAt = now,
            });
        }
        else
        {
            variable.ValueJson = jsonValue;
            variable.SetAt = now;
        }
    }

    private async Task ExecuteScriptTaskAsync(
        BpmExecutionJob job,
        BpmInstance instance,
        string? configJson,
        CancellationToken ct)
    {
        // 1. Получаем тело сценария: сначала из configJson (inline), потом из BpmScriptModule
        var scriptCode = await ResolveScriptCodeAsync(job, configJson, ct);

        if (string.IsNullOrWhiteSpace(scriptCode))
        {
            _logger.LogWarning(
                "ScriptTask [{ElementId}]: тело сценария не найдено (ни inline, ни в BpmScriptModule). " +
                "Узел пропускается как выполненный.",
                job.ElementId);
            return;
        }

        // 2. Загружаем переменные экземпляра для контекста
        var variables = await _db.BpmInstanceVariables
            .AsNoTracking()
            .Where(v => v.InstanceId == instance.Id)
            .ToDictionaryAsync(v => v.Name, v => v.ValueJson, ct);

        // 3. Собираем контекст
        var httpClient = _httpClientFactory.CreateClient("BpmEngine");
        var context = new ScriptContext(
            instanceId: instance.Id,
            processId: instance.ProcessId,
            processVersionId: instance.ProcessVersionId,
            elementId: job.ElementId,
            variables: variables,
            logger: _logger,
            httpClient: httpClient);

        // 4. Выполняем сценарий
        _logger.LogInformation(
            "ScriptTask [{ElementId}]: запуск сценария (экземпляр {InstanceId})",
            job.ElementId, instance.Id);

        await _scriptExecutor.ExecuteAsync(scriptCode, context, ScriptTimeoutMs, ct);

        // 5. Сохраняем изменённые переменные
        var outputVars = context.OutputVariables;
        if (outputVars.Count > 0)
        {
            var varNames = outputVars.Select(v => v.Name).ToList();
            var existingVars = await _db.BpmInstanceVariables
                .Where(v => v.InstanceId == instance.Id && varNames.Contains(v.Name))
                .ToDictionaryAsync(v => v.Name, ct);

            var now = DateTimeOffset.UtcNow;
            foreach (var (name, value) in outputVars)
            {
                if (existingVars.TryGetValue(name, out var existing))
                {
                    existing.ValueJson = value;
                    existing.SetAt = now;
                }
                else
                {
                    _db.BpmInstanceVariables.Add(new BpmInstanceVariable
                    {
                        Id = Guid.NewGuid(),
                        InstanceId = instance.Id,
                        Name = name,
                        ValueJson = value,
                        SetAt = now,
                    });
                }
            }

            _logger.LogInformation(
                "ScriptTask [{ElementId}]: сохранено {Count} выходных переменных",
                job.ElementId, outputVars.Count);
        }
    }

    /// <summary>
    /// Разрешает тело сценария для ScriptTask:
    /// 1) Inline поле <c>script</c> в configJson элемента.
    /// 2) Модуль сценариев <see cref="BpmScriptModule"/> для версии процесса.
    /// </summary>
    private async Task<string?> ResolveScriptCodeAsync(
        BpmExecutionJob job,
        string? configJson,
        CancellationToken ct)
    {
        // Попытка 1: inline-скрипт в конфигурации элемента
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(configJson);
                if (doc.RootElement.TryGetProperty("script", out var scriptProp))
                {
                    var inline = scriptProp.GetString();
                    if (!string.IsNullOrWhiteSpace(inline))
                    {
                        _logger.LogDebug("ScriptTask [{ElementId}]: используется inline-сценарий", job.ElementId);
                        return inline;
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "ScriptTask [{ElementId}]: ошибка разбора configJson", job.ElementId);
            }
        }

        // Попытка 2: BpmScriptModule для версии процесса
        var module = await _db.BpmScriptModules
            .AsNoTracking()
            .Where(m => m.ProcessVersionId == job.ProcessVersionId && m.PublishedAt != null)
            .OrderByDescending(m => m.PublishedAt)
            .FirstOrDefaultAsync(ct);

        if (module != null && !string.IsNullOrWhiteSpace(module.ScriptBody))
        {
            _logger.LogDebug(
                "ScriptTask [{ElementId}]: используется BpmScriptModule {ModuleId}",
                job.ElementId, module.Id);
            return module.ScriptBody;
        }

        return null;
    }

    private void ExecuteBusinessRuleTaskStub(BpmExecutionJob job)
    {
        _logger.LogInformation(
            "BusinessRuleTask [{ElementId}]: выполнение DMN через движок отложено. Узел считается завершённым.",
            job.ElementId);
    }

    // ─── Вспомогательные методы ───────────────────────────────────────────────

    private async Task<BpmInstance?> LoadInstanceAsync(Guid instanceId, CancellationToken ct)
    {
        return await _db.BpmInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == instanceId, ct);
    }

    private async Task<ProcessModel?> ParseModelAsync(Guid processVersionId, CancellationToken ct)
    {
        var version = await _db.BpmProcessVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == processVersionId, ct);

        if (version == null || string.IsNullOrWhiteSpace(version.DiagramXml))
        {
            _logger.LogWarning("ParseModelAsync: версия {VersionId} не найдена или XML пуст", processVersionId);
            return null;
        }

        try
        {
            return ParseXml(version.Id, version.DiagramXml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка разбора BPMN XML для версии {VersionId}", processVersionId);
            return null;
        }
    }

    private static ProcessModel ParseXml(Guid versionId, string xml)
    {
        var doc = XDocument.Parse(xml);
        var ns = Bpmn;

        // Ищем первый process-элемент
        var process = doc.Descendants(ns + "process").FirstOrDefault()
                   ?? doc.Descendants("process").FirstOrDefault();

        if (process == null)
            return new ProcessModel(versionId, [], [], []);

        var elements = new List<ProcessElement>();
        var flows = new List<SequenceFlowInfo>();

        foreach (var el in process.Elements())
        {
            var localName = el.Name.LocalName;
            var id = el.Attribute("id")?.Value;
            if (id == null) continue;

            var name = el.Attribute("name")?.Value;

            if (localName == "sequenceFlow")
            {
                var sourceRef = el.Attribute("sourceRef")?.Value;
                var targetRef = el.Attribute("targetRef")?.Value;
                if (sourceRef != null && targetRef != null)
                {
                    // Выражение условия
                    var condEl = el.Element(ns + "conditionExpression")
                               ?? el.Element("conditionExpression");
                    var condition = condEl?.Value?.Trim();

                    flows.Add(new SequenceFlowInfo(id, sourceRef, targetRef, condition));
                }
            }
            else
            {
                // Определяем тип события (signal/message/timer)
                string? signalCode = null;
                string? messageCode = null;

                if (localName.Contains("Event", StringComparison.OrdinalIgnoreCase))
                {
                    var signalRef = el.Descendants(ns + "signalEventDefinition")
                                     .Concat(el.Descendants("signalEventDefinition"))
                                     .FirstOrDefault()?.Attribute("signalRef")?.Value;
                    if (signalRef != null)
                        signalCode = signalRef;

                    var msgRef = el.Descendants(ns + "messageEventDefinition")
                                   .Concat(el.Descendants("messageEventDefinition"))
                                   .FirstOrDefault()?.Attribute("messageRef")?.Value;
                    if (msgRef != null)
                        messageCode = msgRef;
                }

                elements.Add(new ProcessElement(id, localName, name, signalCode, messageCode));
            }
        }

        // Также обрабатываем subProcess (плоский проход без рекурсии — MVP)
        foreach (var sub in process.Elements(ns + "subProcess").Concat(process.Elements("subProcess")))
        {
            foreach (var el in sub.Elements())
            {
                var localName = el.Name.LocalName;
                var id = el.Attribute("id")?.Value;
                if (id == null) continue;
                var name = el.Attribute("name")?.Value;

                if (localName == "sequenceFlow")
                {
                    var sourceRef = el.Attribute("sourceRef")?.Value;
                    var targetRef = el.Attribute("targetRef")?.Value;
                    if (sourceRef != null && targetRef != null)
                    {
                        var condEl = el.Element(ns + "conditionExpression") ?? el.Element("conditionExpression");
                        flows.Add(new SequenceFlowInfo(id, sourceRef, targetRef, condEl?.Value?.Trim()));
                    }
                }
                else
                {
                    elements.Add(new ProcessElement(id, localName, name, null, null));
                }
            }
        }

        var startEvents = elements.Where(e => e.ElementType == "startEvent").ToList();
        return new ProcessModel(versionId, elements, flows, startEvents);
    }

    private async Task CreateOrUpdateTokenAsync(
        Guid instanceId,
        string elementId,
        string elementType,
        string? elementName,
        BpmTokenStatus status,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existing = await _db.BpmTokens
            .FirstOrDefaultAsync(t => t.InstanceId == instanceId && t.ElementId == elementId && t.Status != BpmTokenStatus.Completed, ct);

        if (existing != null)
        {
            existing.Status = status;
            if (status == BpmTokenStatus.Completed)
                existing.CompletedAt = now;
        }
        else
        {
            _db.BpmTokens.Add(new BpmToken
            {
                Id = Guid.NewGuid(),
                InstanceId = instanceId,
                ElementId = elementId,
                ElementType = elementType,
                ElementName = elementName,
                Status = status,
                CreatedAt = now,
                CompletedAt = status == BpmTokenStatus.Completed ? now : null,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task CompleteTokenAsync(Guid instanceId, string elementId, DateTimeOffset now, CancellationToken ct)
    {
        var token = await _db.BpmTokens
            .FirstOrDefaultAsync(t =>
                t.InstanceId == instanceId &&
                t.ElementId == elementId &&
                t.Status != BpmTokenStatus.Completed, ct);

        if (token != null)
        {
            token.Status = BpmTokenStatus.Completed;
            token.CompletedAt = now;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Вычисляет простое булево выражение условия против словаря переменных.</summary>
    private static bool EvaluateCondition(string expression, Dictionary<string, string?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;

        // Убираем обёртки JUEL/FEEL/JavaScript: ${...} или #{...}
        var expr = expression.Trim();
        if (expr.StartsWith("${") && expr.EndsWith("}"))
            expr = expr[2..^1].Trim();
        else if (expr.StartsWith("#{") && expr.EndsWith("}"))
            expr = expr[2..^1].Trim();

        // Поддерживаемые операторы: ==, !=, >=, <=, >, <
        var operators = new[] { "==", "!=", ">=", "<=", ">", "<" };
        foreach (var op in operators)
        {
            var idx = expr.IndexOf(op, StringComparison.Ordinal);
            if (idx <= 0) continue;

            var left = expr[..idx].Trim();
            var right = expr[(idx + op.Length)..].Trim().Trim('\'', '"');

            // Ищем значение переменной
            var varValue = variables.TryGetValue(left, out var v) ? v?.Trim('\'', '"', ' ') : null;

            return op switch
            {
                "==" => string.Equals(varValue, right, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(varValue, right, StringComparison.OrdinalIgnoreCase),
                ">=" => double.TryParse(varValue, out var lv) && double.TryParse(right, out var rv) && lv >= rv,
                "<=" => double.TryParse(varValue, out var lv2) && double.TryParse(right, out var rv2) && lv2 <= rv2,
                ">"  => double.TryParse(varValue, out var lv3) && double.TryParse(right, out var rv3) && lv3 > rv3,
                "<"  => double.TryParse(varValue, out var lv4) && double.TryParse(right, out var rv4) && lv4 < rv4,
                _    => false,
            };
        }

        // Булевы выражения
        if (bool.TryParse(expr, out var boolVal)) return boolVal;

        // Проверка наличия переменной
        if (variables.TryGetValue(expr, out var boolVar))
        {
            if (bool.TryParse(boolVar, out var parsedBool)) return parsedBool;
            return boolVar != null;
        }

        return false;
    }

    private static string SubstituteVariables(string template, Dictionary<string, string> variables)
    {
        foreach (var kv in variables)
            template = template.Replace("{{" + kv.Key + "}}", kv.Value);
        return template;
    }

    private static BpmTokenDto MapTokenToDto(BpmToken t) =>
        new(t.Id, t.InstanceId, t.ElementId, t.ElementType, t.ElementName, t.Status, t.SignalCode, t.MessageCode, t.CreatedAt, t.CompletedAt);

    // ─── Внутренние модели парсинга ───────────────────────────────────────────

    private record ProcessModel(
        Guid VersionId,
        IReadOnlyList<ProcessElement> AllElements,
        IReadOnlyList<SequenceFlowInfo> SequenceFlows,
        IReadOnlyList<ProcessElement> StartEvents);

    private record ProcessElement(
        string Id,
        string ElementType,
        string? Name,
        string? SignalCode,
        string? MessageCode);

    private record SequenceFlowInfo(
        string Id,
        string SourceRef,
        string TargetRef,
        string? ConditionExpression);
}
