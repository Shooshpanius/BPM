using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using CoreBPM.Server.Application.Bpm.DTOs;
using CoreBPM.Server.Application.Bpm.Interfaces;
using CoreBPM.Server.Application.Bpm.Scripting;
using CoreBPM.Server.Application.Rules.DTOs;
using CoreBPM.Server.Application.Rules.Interfaces;
using CoreBPM.Server.Domain.Bpm;
using CoreBPM.Server.Domain.Tasks;
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
    private readonly IBpmNotificationService _notificationService;
    private readonly IDmnService _dmnService;
    private readonly ILogger<BpmExecutionEngine> _logger;

    public BpmExecutionEngine(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IBpmScriptExecutor scriptExecutor,
        IBpmNotificationService notificationService,
        IDmnService dmnService,
        ILogger<BpmExecutionEngine> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _scriptExecutor = scriptExecutor;
        _notificationService = notificationService;
        _dmnService = dmnService;
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
            _logger.LogDebug("Нет исходящих потоков от {ElementId} в экземпляре {InstanceId}",
                SanitizeForLog(fromElementId), instanceId);

            // Проверяем, не нужно ли завершить экземпляр (если все токены завершены)
            await CheckAndCompleteInstanceIfFinishedAsync(instance, model, now, ct);
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

            // AND-Join: для параллельных/инклюзивных шлюзов с несколькими входящими потоками
            if (target.ElementType is "parallelGateway" or "inclusiveGateway" or "complexGateway")
            {
                var incomingCount = model.SequenceFlows.Count(f => f.TargetRef == target.Id);
                if (incomingCount > 1)
                {
                    // Токен прибыл — увеличиваем счётчик или создаём новый
                    var canProceed = await HandleJoinCounterAsync(instanceId, target.Id, incomingCount, now, ct);
                    if (!canProceed) continue; // Ждём оставшихся токенов
                }
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

        // Завершаем связанную задачу task_items (если была создана автоматически)
        if (token.LinkedTaskItemId.HasValue)
        {
            var linkedTask = await _db.TaskItems
                .FirstOrDefaultAsync(t => t.Id == token.LinkedTaskItemId.Value, ct);
            if (linkedTask != null && linkedTask.Status != Domain.Tasks.TaskStatus.Done)
            {
                var oldStatus = linkedTask.Status;
                linkedTask.Status = Domain.Tasks.TaskStatus.Done;
                linkedTask.UpdatedAt = now;
                _db.TaskHistoryEntries.Add(new TaskHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    TaskId = linkedTask.Id,
                    ActorUserId = actorUserId,
                    Action = TaskHistoryAction.StatusChanged,
                    FieldName = "Status",
                    OldValue = oldStatus.ToString(),
                    NewValue = Domain.Tasks.TaskStatus.Done.ToString(),
                    CreatedAt = now,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        var result = MapTokenToDto(token);

        // Продвигаем поток вперёд
        await AdvanceFromAsync(instanceId, elementId, ct);

        return result;
    }

    /// <inheritdoc />
    public async Task SendSignalAsync(string signalCode, Dictionary<string, string>? variables = null, CancellationToken ct = default)
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

        // Запускаем новые экземпляры для процессов с сигнальным стартовым событием
        await StartInstancesForSignalAsync(signalCode, variables, now, ct);
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string messageCode, string? correlationKey, Dictionary<string, string>? variables = null, CancellationToken ct = default)
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

        // Запускаем новые экземпляры для процессов с сообщённым стартовым событием
        await StartInstancesForMessageAsync(messageCode, correlationKey, variables, now, ct);
    }

    /// <summary>
    /// Ищет активные версии процессов с сигнальными стартовыми событиями и запускает новые экземпляры.
    /// </summary>
    private async Task StartInstancesForSignalAsync(
        string signalCode,
        Dictionary<string, string>? variables,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Загружаем все активные версии процессов с XML
        var activeVersions = await _db.BpmProcessVersions
            .AsNoTracking()
            .Include(v => v.Process)
            .Where(v => v.Status == BpmProcessVersionStatus.Active && v.DiagramXml != null)
            .ToListAsync(ct);

        foreach (var version in activeVersions)
        {
            ProcessModel model;
            try { model = ParseXml(version.Id, version.DiagramXml!); }
            catch { continue; }

            // Ищем стартовые события с соответствующим SignalCode
            var matchingStarts = model.StartEvents
                .Where(e => string.Equals(e.SignalCode, signalCode, StringComparison.Ordinal))
                .ToList();

            foreach (var startEl in matchingStarts)
            {
                var instance = new BpmInstance
                {
                    Id = Guid.NewGuid(),
                    ProcessId = version.ProcessId,
                    ProcessVersionId = version.Id,
                    Name = $"{version.Process?.Name ?? version.ProcessId.ToString()} — сигнал «{signalCode}» {now:dd.MM.yyyy HH:mm}",
                    State = BpmInstanceState.Active,
                    LaunchSource = BpmInstanceLaunchSource.Signal,
                    StartedAt = now,
                    UpdatedAt = now,
                };
                _db.BpmInstances.Add(instance);

                // Передаём входные переменные в новый экземпляр
                if (variables != null)
                {
                    foreach (var kv in variables)
                    {
                        _db.BpmInstanceVariables.Add(new BpmInstanceVariable
                        {
                            Id = Guid.NewGuid(),
                            InstanceId = instance.Id,
                            Name = kv.Key,
                            ValueJson = JsonSerializer.Serialize(kv.Value),
                            SetAt = now,
                        });
                    }
                }

                await _db.SaveChangesAsync(ct);

                // Санируем код сигнала перед записью в лог
                var safeSignalCode = signalCode.Replace('\n', '_').Replace('\r', '_');
                _logger.LogInformation(
                    "Signal «{SignalCode}»: запущен экземпляр {InstanceId} процесса {ProcessId} (стартовый элемент «{ElementId}»)",
                    safeSignalCode, instance.Id, version.ProcessId, startEl.Id);

                _ = StartAsync(instance.Id, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Ищет активные версии процессов с сообщёнными стартовыми событиями и запускает новые экземпляры.
    /// </summary>
    private async Task StartInstancesForMessageAsync(
        string messageCode,
        string? correlationKey,
        Dictionary<string, string>? variables,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var activeVersions = await _db.BpmProcessVersions
            .AsNoTracking()
            .Include(v => v.Process)
            .Where(v => v.Status == BpmProcessVersionStatus.Active && v.DiagramXml != null)
            .ToListAsync(ct);

        foreach (var version in activeVersions)
        {
            ProcessModel model;
            try { model = ParseXml(version.Id, version.DiagramXml!); }
            catch { continue; }

            var matchingStarts = model.StartEvents
                .Where(e => string.Equals(e.MessageCode, messageCode, StringComparison.Ordinal))
                .ToList();

            foreach (var startEl in matchingStarts)
            {
                var instance = new BpmInstance
                {
                    Id = Guid.NewGuid(),
                    ProcessId = version.ProcessId,
                    ProcessVersionId = version.Id,
                    Name = $"{version.Process?.Name ?? version.ProcessId.ToString()} — сообщение «{messageCode}» {now:dd.MM.yyyy HH:mm}",
                    State = BpmInstanceState.Active,
                    LaunchSource = BpmInstanceLaunchSource.Message,
                    ExternalReference = correlationKey,
                    StartedAt = now,
                    UpdatedAt = now,
                };
                _db.BpmInstances.Add(instance);

                if (variables != null)
                {
                    foreach (var kv in variables)
                    {
                        _db.BpmInstanceVariables.Add(new BpmInstanceVariable
                        {
                            Id = Guid.NewGuid(),
                            InstanceId = instance.Id,
                            Name = kv.Key,
                            ValueJson = JsonSerializer.Serialize(kv.Value),
                            SetAt = now,
                        });
                    }
                }

                await _db.SaveChangesAsync(ct);

                // Санируем код сообщения перед записью в лог
                var safeMessageCode = messageCode.Replace('\n', '_').Replace('\r', '_');
                _logger.LogInformation(
                    "Message «{MessageCode}»: запущен экземпляр {InstanceId} процесса {ProcessId} (стартовый элемент «{ElementId}»)",
                    safeMessageCode, instance.Id, version.ProcessId, startEl.Id);

                _ = StartAsync(instance.Id, CancellationToken.None);
            }
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

            // Задания с IsTimer завершают себя внутри FinalizeTimerTokenAsync — пропускаем
            if (job.Status == BpmJobStatus.Completed) return;

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

                await _db.SaveChangesAsync(ct);

                // Проверяем наличие граничного события ошибки
                var boundaryActivated = await TryActivateBoundaryErrorEventAsync(
                    instance, job.ElementId, job.LastError, now, ct);

                if (!boundaryActivated)
                {
                    // Нет граничного события — переводим экземпляр в Faulted
                    var tracked = await _db.BpmInstances.FirstOrDefaultAsync(i => i.Id == instance.Id, ct);
                    if (tracked != null)
                    {
                        tracked.State = BpmInstanceState.Faulted;
                        tracked.UpdatedAt = now;
                        await _db.SaveChangesAsync(ct);
                    }
                }
            }
            else
            {
                // Планируем повторную попытку с экспоненциальной задержкой
                var delaySeconds = Math.Min(300, (int)Math.Pow(2, job.AttemptNumber) * 10);
                job.Status = BpmJobStatus.Scheduled;
                job.NextRunAt = now.AddSeconds(delaySeconds);
                await _db.SaveChangesAsync(ct);
            }
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
                await HandleEndEventAsync(instance, elementId, elementName, now, ct, model);
                break;

            case "userTask":
            case "receiveTask":
                await HandleUserTaskAsync(instance, model, elementId, elementType, elementName, now, ct);
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
                await HandleCallActivityAsync(instance, model, elementId, elementName, now, ct);
                break;

            case "subProcess":
                await HandleSubProcessAsync(instance, model, elementId, elementName, now, ct);
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
        CancellationToken ct,
        ProcessModel? model = null)
    {
        // Проверяем, является ли этот endEvent концом embedded subProcess
        if (model != null)
        {
            var endEventNode = model.AllElements.FirstOrDefault(e => e.Id == elementId);
            if (endEventNode?.ContainerElementId != null)
            {
                // Это конец subProcess — завершаем только subprocess-контейнер и продвигаем от него
                var subProcessId = endEventNode.ContainerElementId;
                await CreateOrUpdateTokenAsync(instance.Id, elementId, "endEvent", elementName, BpmTokenStatus.Completed, now, ct);

                _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    EventType = BpmHistoryEventType.NodeExecuted,
                    ElementId = elementId,
                    ElementName = elementName,
                    Text = $"Подпроцесс завершён (endEvent «{elementName ?? elementId}»)",
                    OccurredAt = now,
                });
                await _db.SaveChangesAsync(ct);

                // Завершаем токен самого subProcess и продвигаемся от него
                await CompleteTokenAsync(instance.Id, subProcessId, now, ct);
                await AdvanceFromAsync(instance.Id, subProcessId, ct);
                return;
            }
        }

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

        // Если это дочерний экземпляр (callActivity) — продвигаем родительский поток
        if (tracked?.ParentInstanceId != null)
        {
            await ResumeParentCallActivityAsync(tracked.ParentInstanceId.Value, instance.Id, now, ct);
        }
    }

    private async Task ResumeParentCallActivityAsync(
        Guid parentInstanceId,
        Guid childInstanceId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Ищем токен родителя в статусе WaitingCallActivity
        var callActivityToken = await _db.BpmTokens
            .FirstOrDefaultAsync(t =>
                t.InstanceId == parentInstanceId &&
                t.Status == BpmTokenStatus.WaitingCallActivity, ct);

        if (callActivityToken == null)
        {
            _logger.LogWarning(
                "ResumeParent: не найден WaitingCallActivity токен в родительском экземпляре {ParentId} для дочернего {ChildId}",
                parentInstanceId, childInstanceId);
            return;
        }

        var callActivityElementId = callActivityToken.ElementId;
        callActivityToken.Status = BpmTokenStatus.Completed;
        callActivityToken.CompletedAt = now;

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = parentInstanceId,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = callActivityElementId,
            ElementName = callActivityToken.ElementName,
            Text = $"CallActivity «{callActivityToken.ElementName ?? callActivityElementId}» завершён (дочерний экземпляр {childInstanceId})",
            OccurredAt = now,
        });

        // Применяем выходные маппинги переменных (outputMappings из конфигурации CallActivity)
        await ApplyCallActivityOutputMappingsAsync(parentInstanceId, childInstanceId, callActivityElementId, now, ct);

        await _db.SaveChangesAsync(ct);

        // Продвигаем родительский поток
        await AdvanceFromAsync(parentInstanceId, callActivityElementId, ct);
    }

    private async Task HandleCallActivityAsync(
        BpmInstance instance,
        ProcessModel model,
        string elementId,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var callActivityNode = model.AllElements.FirstOrDefault(e => e.Id == elementId);
        var calledElement = callActivityNode?.CalledElement;

        // Ищем вызываемый процесс: сначала по GUID, потом по Name, затем по DataClassName
        BpmProcess? calledProcess = null;
        if (!string.IsNullOrWhiteSpace(calledElement))
        {
            if (Guid.TryParse(calledElement, out var calledProcessId))
            {
                calledProcess = await _db.BpmProcesses.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == calledProcessId && !p.IsDeleted, ct);
            }
            calledProcess ??= await _db.BpmProcesses.AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    (p.Name == calledElement || p.DataClassName == calledElement) && !p.IsDeleted, ct);
        }

        if (calledProcess == null)
        {
            _logger.LogWarning(
                "CallActivity [{ElementId}]: вызываемый процесс «{CalledElement}» не найден — узел обрабатывается как выполненный",
                elementId, calledElement ?? "(не задан)");
            await CreateOrUpdateTokenAsync(instance.Id, elementId, "callActivity", elementName, BpmTokenStatus.Completed, now, ct);
            _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                EventType = BpmHistoryEventType.NodeExecuted,
                ElementId = elementId,
                ElementName = elementName,
                Text = $"CallActivity «{elementName ?? elementId}»: вызываемый процесс не найден, пропуск",
                OccurredAt = now,
            });
            await _db.SaveChangesAsync(ct);
            await AdvanceFromAsync(instance.Id, elementId, ct);
            return;
        }

        // Ищем активную версию вызываемого процесса
        var calledVersion = await _db.BpmProcessVersions
            .AsNoTracking()
            .Where(v => v.ProcessId == calledProcess.Id && v.Status == BpmProcessVersionStatus.Active)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (calledVersion == null)
        {
            _logger.LogWarning(
                "CallActivity [{ElementId}]: процесс «{ProcessName}» не имеет активной версии — пропуск",
                elementId, calledProcess.Name);
            await CreateOrUpdateTokenAsync(instance.Id, elementId, "callActivity", elementName, BpmTokenStatus.Completed, now, ct);
            _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                EventType = BpmHistoryEventType.NodeExecuted,
                ElementId = elementId,
                ElementName = elementName,
                Text = $"CallActivity «{elementName ?? elementId}»: нет активной версии процесса, пропуск",
                OccurredAt = now,
            });
            await _db.SaveChangesAsync(ct);
            await AdvanceFromAsync(instance.Id, elementId, ct);
            return;
        }

        // Создаём дочерний экземпляр
        var childInstance = new BpmInstance
        {
            Id = Guid.NewGuid(),
            ProcessId = calledProcess.Id,
            ProcessVersionId = calledVersion.Id,
            Name = $"{calledProcess.Name} (из {instance.Name})",
            State = BpmInstanceState.Active,
            LaunchSource = BpmInstanceLaunchSource.CallActivity,
            InitiatorUserId = instance.InitiatorUserId,
            ResponsibleUserId = instance.ResponsibleUserId,
            ParentInstanceId = instance.Id,
            StartedAt = now,
            UpdatedAt = now,
        };
        _db.BpmInstances.Add(childInstance);

        // Применяем входные маппинги переменных (inputMappings из конфигурации CallActivity)
        await ApplyCallActivityInputMappingsAsync(instance, childInstance, elementId, now, ct);

        // Устанавливаем токен родителя в ожидание
        await CreateOrUpdateTokenAsync(instance.Id, elementId, "callActivity", elementName, BpmTokenStatus.WaitingCallActivity, now, ct);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = elementName,
            Text = $"CallActivity «{elementName ?? elementId}» запустил дочерний процесс «{calledProcess.Name}» (экземпляр {childInstance.Id})",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);

        // Запускаем дочерний экземпляр (fire-and-forget)
        _ = StartAsync(childInstance.Id, CancellationToken.None);
    }

    /// <summary>
    /// Применяет входные маппинги переменных CallActivity: копирует переменные
    /// из родительского экземпляра в дочерний согласно настройкам <c>inputMappings</c>.
    /// Ожидаемый формат ConfigJson: <c>{ "inputMappings": [{ "sourceVar": "x", "targetVar": "y" }] }</c>
    /// </summary>
    private async Task ApplyCallActivityInputMappingsAsync(
        BpmInstance parentInstance,
        BpmInstance childInstance,
        string callActivityElementId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var configJson = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == parentInstance.ProcessId && c.ElementId == callActivityElementId)
            .Select(c => c.ConfigJson)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(configJson)) return;

        List<VariableMapping>? mappings;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (!doc.RootElement.TryGetProperty("inputMappings", out var mappingsEl)) return;
            mappings = JsonSerializer.Deserialize<List<VariableMapping>>(mappingsEl.GetRawText());
        }
        catch
        {
            return;
        }

        if (mappings == null || mappings.Count == 0) return;

        // Загружаем переменные родительского экземпляра
        var sourceNames = mappings.Select(m => m.SourceVar).ToList();
        var parentVars = await _db.BpmInstanceVariables
            .AsNoTracking()
            .Where(v => v.InstanceId == parentInstance.Id && sourceNames.Contains(v.Name))
            .ToDictionaryAsync(v => v.Name, ct);

        foreach (var mapping in mappings)
        {
            if (!parentVars.TryGetValue(mapping.SourceVar, out var sourceVar)) continue;

            _db.BpmInstanceVariables.Add(new BpmInstanceVariable
            {
                Id = Guid.NewGuid(),
                InstanceId = childInstance.Id,
                Name = mapping.TargetVar,
                ValueJson = sourceVar.ValueJson,
                SetAt = now,
            });
        }
    }

    /// <summary>
    /// Применяет выходные маппинги переменных CallActivity: копирует переменные
    /// из завершённого дочернего экземпляра обратно в родительский согласно <c>outputMappings</c>.
    /// Ожидаемый формат ConfigJson: <c>{ "outputMappings": [{ "sourceVar": "result", "targetVar": "parentResult" }] }</c>
    /// </summary>
    private async Task ApplyCallActivityOutputMappingsAsync(
        Guid parentInstanceId,
        Guid childInstanceId,
        string callActivityElementId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Получаем ProcessId родительского экземпляра для поиска конфигурации
        var parentProcessId = await _db.BpmInstances
            .AsNoTracking()
            .Where(i => i.Id == parentInstanceId)
            .Select(i => i.ProcessId)
            .FirstOrDefaultAsync(ct);

        if (parentProcessId == Guid.Empty) return;

        var configJson = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == parentProcessId && c.ElementId == callActivityElementId)
            .Select(c => c.ConfigJson)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(configJson)) return;

        List<VariableMapping>? mappings;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (!doc.RootElement.TryGetProperty("outputMappings", out var mappingsEl)) return;
            mappings = JsonSerializer.Deserialize<List<VariableMapping>>(mappingsEl.GetRawText());
        }
        catch
        {
            return;
        }

        if (mappings == null || mappings.Count == 0) return;

        // Загружаем переменные дочернего экземпляра
        var sourceNames = mappings.Select(m => m.SourceVar).ToList();
        var childVars = await _db.BpmInstanceVariables
            .AsNoTracking()
            .Where(v => v.InstanceId == childInstanceId && sourceNames.Contains(v.Name))
            .ToDictionaryAsync(v => v.Name, ct);

        if (childVars.Count == 0) return;

        // Загружаем существующие переменные родительского экземпляра для upsert
        var targetNames = mappings.Select(m => m.TargetVar).ToList();
        var parentVars = await _db.BpmInstanceVariables
            .Where(v => v.InstanceId == parentInstanceId && targetNames.Contains(v.Name))
            .ToDictionaryAsync(v => v.Name, ct);

        foreach (var mapping in mappings)
        {
            if (!childVars.TryGetValue(mapping.SourceVar, out var sourceVar)) continue;

            if (parentVars.TryGetValue(mapping.TargetVar, out var existing))
            {
                existing.ValueJson = sourceVar.ValueJson;
                existing.SetAt = now;
            }
            else
            {
                _db.BpmInstanceVariables.Add(new BpmInstanceVariable
                {
                    Id = Guid.NewGuid(),
                    InstanceId = parentInstanceId,
                    Name = mapping.TargetVar,
                    ValueJson = sourceVar.ValueJson,
                    SetAt = now,
                });
            }
        }
    }

    private async Task HandleSubProcessAsync(
        BpmInstance instance,
        ProcessModel model,
        string elementId,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Устанавливаем токен субпроцесса в Active
        await CreateOrUpdateTokenAsync(instance.Id, elementId, "subProcess", elementName, BpmTokenStatus.Active, now, ct);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = elementName,
            Text = $"Подпроцесс «{elementName ?? elementId}» начат",
            OccurredAt = now,
        });
        await _db.SaveChangesAsync(ct);

        // Ищем startEvent внутри subProcess (ContainerElementId == elementId)
        var internalStartEvents = model.AllElements
            .Where(e => e.ElementType == "startEvent" && e.ContainerElementId == elementId)
            .ToList();

        if (internalStartEvents.Count == 0)
        {
            _logger.LogWarning(
                "SubProcess [{ElementId}]: нет внутреннего startEvent — субпроцесс завершается немедленно",
                elementId);
            await CompleteTokenAsync(instance.Id, elementId, now, ct);
            await AdvanceFromAsync(instance.Id, elementId, ct);
            return;
        }

        // Активируем каждый внутренний стартовый узел
        foreach (var startEvent in internalStartEvents)
        {
            await ExecuteElementAsync(instance, model, startEvent.Id, startEvent.ElementType, startEvent.Name, now, ct);
        }
    }

    private async Task HandleUserTaskAsync(
        BpmInstance instance,
        ProcessModel model,
        string elementId,
        string elementType,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await CreateOrUpdateTokenAsync(instance.Id, elementId, elementType, elementName, BpmTokenStatus.WaitingUserAction, now, ct);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = elementName,
            Text = $"Пользовательская задача «{elementName ?? elementId}» ожидает выполнения",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);

        // Создаём задачу task_items, связанную с этим UserTask-узлом
        await TryCreateLinkedTaskItemAsync(instance, elementId, elementName, now, ct);

        // Планируем граничные таймерные события для этой задачи
        await TryScheduleBoundaryTimerEventsAsync(instance, model, elementId, now, ct);

        // Уведомляем участников процесса о появлении новой задачи
        try
        {
            var instanceWithProcess = await _db.BpmInstances
                .AsNoTracking()
                .Include(i => i.Process)
                .FirstOrDefaultAsync(i => i.Id == instance.Id, ct);

            if (instanceWithProcess != null)
            {
                await _notificationService.NotifyUserTaskActivatedAsync(
                    instance.Id,
                    instanceWithProcess.Name,
                    instanceWithProcess.Process?.Name ?? string.Empty,
                    elementId,
                    elementName,
                    ct);
            }
        }
        catch (Exception ex)
        {
            // Уведомление не критично — не прерываем выполнение
            _logger.LogWarning(ex, "Ошибка отправки уведомления о UserTask {ElementId}", elementId);
        }
    }

    /// <summary>
    /// Создаёт запись task_items для UserTask-узла и проставляет LinkedTaskItemId в токен.
    /// Разрешает исполнителя, срок и приоритет из конфига элемента и переменных экземпляра.
    /// </summary>
    private async Task TryCreateLinkedTaskItemAsync(
        BpmInstance instance,
        string elementId,
        string? elementName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        try
        {
            // Загружаем конфиг элемента
            var configJson = await _db.BpmElementConfigs
                .AsNoTracking()
                .Where(c => c.ProcessId == instance.ProcessId && c.ElementId == elementId)
                .Select(c => c.ConfigJson)
                .FirstOrDefaultAsync(ct);

            UserTaskConfig config;
            try
            {
                config = string.IsNullOrWhiteSpace(configJson)
                    ? new UserTaskConfig()
                    : JsonSerializer.Deserialize<UserTaskConfig>(configJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? new UserTaskConfig();
            }
            catch
            {
                config = new UserTaskConfig();
            }

            // Загружаем переменные экземпляра (нужны для Expression/Variable резолюции)
            var variables = await _db.BpmInstanceVariables
                .AsNoTracking()
                .Where(v => v.InstanceId == instance.Id)
                .ToDictionaryAsync(v => v.Name, v => v.ValueJson, StringComparer.OrdinalIgnoreCase, ct);

            // Резолюция исполнителя
            Guid? assigneeId = await ResolveAssigneeAsync(config, variables, ct);
            if (assigneeId == null)
            {
                _logger.LogWarning("HandleUserTask: не удалось разрешить исполнителя для {ElementId}", SanitizeForLog(elementId));
                return;
            }

            // Резолюция срока
            DateTimeOffset dueDate = ResolveUserTaskDueDate(config, variables, now);

            // Маппинг приоритета
            TaskPriority priority = config.Priority?.ToLowerInvariant() switch
            {
                "low" => TaskPriority.Low,
                "high" => TaskPriority.High,
                "critical" => TaskPriority.Critical,
                _ => TaskPriority.Medium,
            };

            // Загружаем имя процесса для формирования темы
            var processName = await _db.BpmProcesses
                .AsNoTracking()
                .Where(p => p.Id == instance.ProcessId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct);

            var subject = !string.IsNullOrWhiteSpace(elementName)
                ? elementName
                : $"Задача из процесса «{processName ?? instance.ProcessId.ToString()}»";

            var maxNum = await _db.TaskItems.MaxAsync(t => (int?)t.Number, ct) ?? 0;

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Number = maxNum + 1,
                Subject = subject,
                Status = Domain.Tasks.TaskStatus.New,
                Priority = priority,
                AuthorUserId = assigneeId.Value, // автор = исполнитель при автосоздании
                AssigneeUserId = assigneeId.Value,
                StartDate = now,
                DueDate = dueDate,
                SourceInstanceId = instance.Id,
                SourceElementId = elementId,
                IsOverdue = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _db.TaskItems.Add(task);
            _db.TaskHistoryEntries.Add(new TaskHistoryEntry
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                ActorUserId = assigneeId.Value,
                Action = TaskHistoryAction.Created,
                NewValue = $"Создана из процесса, экземпляр {instance.Id}, узел {elementId}",
                CreatedAt = now,
            });

            // Проставляем LinkedTaskItemId в токен
            var token = await _db.BpmTokens
                .FirstOrDefaultAsync(t => t.InstanceId == instance.Id && t.ElementId == elementId && t.Status == BpmTokenStatus.WaitingUserAction, ct);
            if (token != null)
                token.LinkedTaskItemId = task.Id;

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Создание задачи не критично — не прерываем выполнение процесса
            _logger.LogWarning(ex, "Ошибка автосоздания TaskItem для UserTask {ElementId}", SanitizeForLog(elementId));
        }
    }

    /// <summary>Разрешает исполнителя UserTask из конфига (User / Role / Expression).</summary>
    private async Task<Guid?> ResolveAssigneeAsync(
        UserTaskConfig config,
        Dictionary<string, string?> variables,
        CancellationToken ct)
    {
        var assigneeType = config.AssigneeType ?? "User";
        var assigneeValue = config.AssigneeValue ?? string.Empty;

        if (assigneeType.Equals("Expression", StringComparison.OrdinalIgnoreCase))
        {
            // Подставляем переменную формата ${varName} или просто имя переменной
            var varName = assigneeValue.TrimStart('$', '{').TrimEnd('}').Trim();
            variables.TryGetValue(varName, out var varVal);
            assigneeValue = varVal?.Trim('"') ?? string.Empty;
            // После подстановки обрабатываем как User
            assigneeType = "User";
        }

        if (assigneeType.Equals("User", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(assigneeValue)) return null;

            // Пробуем разобрать как GUID
            if (Guid.TryParse(assigneeValue, out var userId))
            {
                var exists = await _db.OrgUsers.AnyAsync(u => u.Id == userId, ct);
                return exists ? userId : null;
            }

            // Иначе ищем по email (через AuthAccount)
            var account = await _db.AuthAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Username == assigneeValue, ct);
            return account?.UserId;
        }

        if (assigneeType.Equals("Role", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(assigneeValue)) return null;

            // Ищем первого пользователя с заданной ролью
            var userRole = await _db.AuthUserRoles
                .AsNoTracking()
                .Include(ur => ur.Role)
                .Include(ur => ur.Account)
                .FirstOrDefaultAsync(ur => ur.Role.Name == assigneeValue, ct);
            return userRole?.Account?.UserId;
        }

        return null;
    }

    /// <summary>Рассчитывает дату завершения задачи из конфига UserTask.</summary>
    private static DateTimeOffset ResolveUserTaskDueDate(
        UserTaskConfig config,
        Dictionary<string, string?> variables,
        DateTimeOffset now)
    {
        var dueDateType = config.DueDateType ?? "Relative";
        var dueDateValue = config.DueDateValue ?? string.Empty;

        if (dueDateType.Equals("Fixed", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTimeOffset.TryParse(dueDateValue, out var fixedDate))
                return fixedDate;
        }
        else if (dueDateType.Equals("Variable", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(dueDateValue))
            {
                variables.TryGetValue(dueDateValue, out var varVal);
                var rawVal = varVal?.Trim('"') ?? string.Empty;
                if (DateTimeOffset.TryParse(rawVal, out var varDate))
                    return varDate;
            }
        }
        else // Relative (по умолчанию)
        {
            if (!string.IsNullOrWhiteSpace(dueDateValue))
            {
                try
                {
                    var duration = System.Xml.XmlConvert.ToTimeSpan(dueDateValue);
                    return now.Add(duration);
                }
                catch { /* некорректный формат — используем значение по умолчанию */ }
            }
        }

        // По умолчанию — 3 рабочих дня
        return now.AddDays(3);
    }

    /// <summary>Внутренняя модель конфига UserTask-элемента (соответствует UserTaskConfig в UserTaskTab.tsx).</summary>
    private sealed class UserTaskConfig
    {
        public string? AssigneeType { get; set; }
        public string? AssigneeValue { get; set; }
        public string? DueDateType { get; set; }
        public string? DueDateValue { get; set; }
        public string? Priority { get; set; }
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

        // Планируем граничные таймерные события для этой задачи
        await TryScheduleBoundaryTimerEventsAsync(instance, model, elementId, now, ct);
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
        var timerDuration = eventNode?.TimerDuration;
        var timerCycle = eventNode?.TimerCycle;
        var timerDate = eventNode?.TimerDate;

        BpmTokenStatus status;
        string historyText;

        if (!string.IsNullOrWhiteSpace(signalCode))
        {
            status = BpmTokenStatus.WaitingSignal;
            historyText = $"Ожидание сигнала «{signalCode}»";
        }
        else if (!string.IsNullOrWhiteSpace(messageCode))
        {
            status = BpmTokenStatus.WaitingMessage;
            historyText = $"Ожидание сообщения «{messageCode}»";
        }
        else if (!string.IsNullOrWhiteSpace(timerDuration) || !string.IsNullOrWhiteSpace(timerDate))
        {
            // Таймерное событие — создаём задание с IsTimer=true
            status = BpmTokenStatus.WaitingTimer;
            var fireAt = ResolveTimerFireAt(now, timerDuration, timerCycle, timerDate);
            historyText = $"Таймерное событие «{elementName ?? elementId}» — срабатывание в {fireAt:u}";

            // Загружаем instance для ProcessId/ProcessVersionId
            var inst = await _db.BpmInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId, ct);
            if (inst != null)
            {
                var timerJob = new BpmExecutionJob
                {
                    Id = Guid.NewGuid(),
                    ProcessId = inst.ProcessId,
                    ProcessVersionId = inst.ProcessVersionId,
                    InstanceId = instanceId,
                    ElementId = elementId,
                    ElementType = "intermediateCatchEvent",
                    OperationName = elementName,
                    Status = BpmJobStatus.Pending,
                    IsTimer = true,
                    MaxAttempts = 1,
                    NextRunAt = fireAt,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                _db.BpmExecutionJobs.Add(timerJob);
            }
        }
        else
        {
            // Неизвестное событие — ждём действия пользователя
            status = BpmTokenStatus.WaitingUserAction;
            historyText = $"Промежуточное событие «{elementName ?? elementId}» ожидает";
        }

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
            Text = historyText,
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
            await SendSignalAsync(signalCode, ct: ct);

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
                await ExecuteBusinessRuleTaskAsync(job, instance, configJson, ct);
                break;

            case "intermediateCatchEvent" when job.IsTimer:
                // Таймер сработал — просто продвигаем токен вперёд
                _logger.LogInformation(
                    "Таймерное событие [{ElementId}] сработало в экземпляре {InstanceId}",
                    job.ElementId, job.InstanceId);
                await FinalizeTimerTokenAsync(instance.Id, job.ElementId, job, DateTimeOffset.UtcNow, ct);
                return; // Не вызываем AdvanceFromAsync из ExecuteJobAsync — уже сделано внутри

            case "boundaryEvent" when job.IsTimer:
                // Граничный таймер сработал — активируем граничное событие
                _logger.LogInformation(
                    "Граничный таймер [{ElementId}] сработал в экземпляре {InstanceId}",
                    job.ElementId, job.InstanceId);
                await TryActivateBoundaryTimerEventAsync(instance, job.ElementId, DateTimeOffset.UtcNow, ct);
                return; // Продвижение потока происходит внутри TryActivateBoundaryTimerEventAsync

            default:
                _logger.LogInformation("Задание типа {ElementType} — пассивное выполнение (заглушка)", job.ElementType);
                break;
        }
    }

    /// <summary>Завершает токен таймерного события и продвигает поток.</summary>
    private async Task FinalizeTimerTokenAsync(
        Guid instanceId,
        string elementId,
        BpmExecutionJob job,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Завершаем токен WaitingTimer
        var timerToken = await _db.BpmTokens
            .FirstOrDefaultAsync(t =>
                t.InstanceId == instanceId &&
                t.ElementId == elementId &&
                t.Status == BpmTokenStatus.WaitingTimer, ct);

        if (timerToken != null)
        {
            timerToken.Status = BpmTokenStatus.Completed;
            timerToken.CompletedAt = now;
        }

        // Помечаем задание как Completed
        job.Status = BpmJobStatus.Completed;
        job.CompletedAt = now;

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instanceId,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = elementId,
            ElementName = timerToken?.ElementName,
            Text = $"Таймерное событие «{timerToken?.ElementName ?? elementId}» сработало",
            OccurredAt = now,
        });

        await _db.SaveChangesAsync(ct);

        // Продвигаем поток дальше
        await AdvanceFromAsync(instanceId, elementId, ct);
    }

    /// <summary>
    /// Рассчитывает время срабатывания таймера:
    /// - timeDuration: ISO 8601 интервал (PT5M, P1D и т.п.)
    /// - timerDate: конкретная дата-время
    /// - timerCycle: первый повтор по ISO 8601 интервалу
    /// </summary>
    private static DateTimeOffset ResolveTimerFireAt(
        DateTimeOffset now,
        string? timerDuration,
        string? timerCycle,
        string? timerDate)
    {
        if (!string.IsNullOrWhiteSpace(timerDate) &&
            DateTimeOffset.TryParse(timerDate, out var specificDate))
        {
            return specificDate;
        }

        // Попытка разобрать timeDuration или timeCycle как ISO 8601 интервал
        var isoStr = timerDuration ?? timerCycle;
        if (!string.IsNullOrWhiteSpace(isoStr))
        {
            try
            {
                var span = System.Xml.XmlConvert.ToTimeSpan(isoStr);
                return now.Add(span);
            }
            catch
            {
                // Если не удалось — 30 минут по умолчанию
            }
        }

        return now.AddMinutes(30);
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

        // 3. Собираем контекст (таймаут берётся из configJson.scriptTimeoutMs или константы)
        var timeoutMs = ScriptTimeoutMs;
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                using var cfgDoc = JsonDocument.Parse(configJson);
                if (cfgDoc.RootElement.TryGetProperty("scriptTimeoutMs", out var toEl) && toEl.TryGetInt32(out var toV) && toV > 0)
                    timeoutMs = toV;
            }
            catch { /* игнорируем ошибки разбора */ }
        }

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
            "ScriptTask [{ElementId}]: запуск сценария (экземпляр {InstanceId}, таймаут {TimeoutMs} мс)",
            job.ElementId, instance.Id, timeoutMs);

        await _scriptExecutor.ExecuteAsync(scriptCode, context, timeoutMs, ct);

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

    private async Task ExecuteBusinessRuleTaskAsync(
        BpmExecutionJob job,
        BpmInstance instance,
        string? configJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            _logger.LogWarning("BusinessRuleTask [{ElementId}]: конфигурация отсутствует, узел пропускается", job.ElementId);
            return;
        }

        Guid tableId;
        Guid versionId;
        List<(Guid ColumnId, string VariableName)> inputMappings;
        List<(Guid ColumnId, string VariableName)> outputMappings;

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tableId", out var tableIdProp) ||
                !Guid.TryParse(tableIdProp.GetString(), out tableId))
            {
                _logger.LogWarning("BusinessRuleTask [{ElementId}]: tableId не задан или невалиден", job.ElementId);
                return;
            }

            if (!root.TryGetProperty("versionId", out var versionIdProp) ||
                !Guid.TryParse(versionIdProp.GetString(), out versionId))
            {
                _logger.LogWarning("BusinessRuleTask [{ElementId}]: versionId не задан, пытаемся получить опубликованную версию", job.ElementId);
                // Ищем опубликованную версию автоматически
                var pubVersion = await _db.DmnTableVersions
                    .AsNoTracking()
                    .Where(v => v.TableId == tableId && v.Status == Domain.Rules.DmnVersionStatus.Published)
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefaultAsync(ct);
                if (pubVersion == null)
                {
                    _logger.LogWarning("BusinessRuleTask [{ElementId}]: нет опубликованных версий DMN-таблицы {TableId}", job.ElementId, tableId);
                    return;
                }
                versionId = pubVersion.Id;
            }

            static List<(Guid, string)> ParseMappings(JsonElement root, string key)
            {
                var result = new List<(Guid, string)>();
                if (!root.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return result;
                foreach (var item in arr.EnumerateArray())
                {
                    if (!item.TryGetProperty("columnId", out var colProp)) continue;
                    if (!Guid.TryParse(colProp.GetString(), out var colId)) continue;
                    var varName = item.TryGetProperty("variableName", out var vnProp) ? vnProp.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(varName))
                        result.Add((colId, varName));
                }
                return result;
            }

            inputMappings = ParseMappings(root, "inputMappings");
            outputMappings = ParseMappings(root, "outputMappings");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "BusinessRuleTask [{ElementId}]: ошибка разбора configJson", job.ElementId);
            return;
        }

        // Загружаем переменные экземпляра
        var instanceVars = await _db.BpmInstanceVariables
            .AsNoTracking()
            .Where(v => v.InstanceId == instance.Id)
            .ToDictionaryAsync(v => v.Name, v => v.ValueJson, ct);

        // Строим входные данные DMN по маппингу (columnId → значение переменной)
        var dmnInputs = new Dictionary<Guid, string?>();
        foreach (var (colId, varName) in inputMappings)
        {
            instanceVars.TryGetValue(varName, out var rawValue);
            // Убираем JSON-обёртку кавычек
            var strValue = rawValue?.Trim('"', ' ');
            dmnInputs[colId] = strValue;
        }

        // Вызываем DMN-движок
        DmnTestResponse dmnResult;
        try
        {
            dmnResult = await _dmnService.EvaluateAsync(
                tableId,
                versionId,
                new DmnTestRequest(dmnInputs),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BusinessRuleTask [{ElementId}]: ошибка вычисления DMN-таблицы {TableId}", job.ElementId, tableId);
            throw;
        }

        if (dmnResult.MatchedRows.Count == 0)
        {
            _logger.LogInformation(
                "BusinessRuleTask [{ElementId}]: DMN не нашёл совпадений — переменные не изменены",
                job.ElementId);
            return;
        }

        // Берём первый результат (First / Unique); для Collect обрабатываем первую строку
        var firstRow = dmnResult.MatchedRows[0];

        if (outputMappings.Count == 0)
        {
            _logger.LogInformation("BusinessRuleTask [{ElementId}]: outputMappings не заданы, результат DMN не записан в переменные", job.ElementId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var varNamesToLoad = outputMappings.Select(m => m.VariableName).ToList();
        var existingVars = await _db.BpmInstanceVariables
            .Where(v => v.InstanceId == instance.Id && varNamesToLoad.Contains(v.Name))
            .ToDictionaryAsync(v => v.Name, ct);

        foreach (var (colId, varName) in outputMappings)
        {
            if (!firstRow.Outputs.TryGetValue(colId, out var outputValue)) continue;
            var jsonValue = outputValue == null ? "null" : $"\"{outputValue}\"";

            if (existingVars.TryGetValue(varName, out var existing))
            {
                existing.ValueJson = jsonValue;
                existing.SetAt = now;
            }
            else
            {
                _db.BpmInstanceVariables.Add(new BpmInstanceVariable
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    Name = varName,
                    ValueJson = jsonValue,
                    SetAt = now,
                });
            }
        }

        _logger.LogInformation(
            "BusinessRuleTask [{ElementId}]: DMN-результат записан в {Count} переменных(ую)",
            job.ElementId, outputMappings.Count);
    }

    /// <summary>
    /// Ищет граничные таймерные события, прикреплённые к задаче, и планирует задания для их выполнения.
    /// </summary>
    private async Task TryScheduleBoundaryTimerEventsAsync(
        BpmInstance instance,
        ProcessModel model,
        string taskElementId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Ищем граничные события с таймером, прикреплённые к данному элементу
        var boundaryTimerEvents = model.AllElements
            .Where(e => e.ElementType == "boundaryEvent"
                     && e.BoundaryFor == taskElementId
                     && (!string.IsNullOrWhiteSpace(e.TimerDuration)
                         || !string.IsNullOrWhiteSpace(e.TimerCycle)
                         || !string.IsNullOrWhiteSpace(e.TimerDate)))
            .ToList();

        if (boundaryTimerEvents.Count == 0)
            return;

        foreach (var be in boundaryTimerEvents)
        {
            var fireAt = ResolveTimerFireAt(now, be.TimerDuration, be.TimerCycle, be.TimerDate);

            var timerJob = new BpmExecutionJob
            {
                Id = Guid.NewGuid(),
                ProcessId = instance.ProcessId,
                ProcessVersionId = model.VersionId,
                InstanceId = instance.Id,
                ElementId = be.Id,
                ElementType = "boundaryEvent",
                OperationName = be.Name,
                Status = BpmJobStatus.Pending,
                IsTimer = true,
                MaxAttempts = 1,
                NextRunAt = fireAt,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.BpmExecutionJobs.Add(timerJob);

            _logger.LogInformation(
                "Запланирован граничный таймер {BoundaryId} для задачи {TaskId} — срабатывание в {FireAt:u}",
                be.Id, taskElementId, fireAt);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Активирует граничное таймерное событие при его срабатывании.
    /// Возвращает true если граничное событие было найдено и активировано.
    /// </summary>
    private async Task<bool> TryActivateBoundaryTimerEventAsync(
        BpmInstance instance,
        string boundaryEventElementId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var model = await ParseModelAsync(instance.ProcessVersionId, ct);
        if (model == null) return false;

        // Ищем описание граничного события в модели
        var be = model.AllElements.FirstOrDefault(e =>
            e.ElementType == "boundaryEvent" && e.Id == boundaryEventElementId);

        if (be == null)
        {
            _logger.LogWarning(
                "Граничное таймерное событие {BoundaryId} не найдено в модели процесса {VersionId}",
                boundaryEventElementId, instance.ProcessVersionId);
            return false;
        }

        _logger.LogInformation(
            "Граничное таймерное событие {BoundaryId} активировано для задачи {TaskId}",
            be.Id, be.BoundaryFor ?? "(не задана)");

        // Если cancelActivity=true (по умолчанию) — отменяем хост-задачу
        if (be.IsCancelActivity != false && !string.IsNullOrWhiteSpace(be.BoundaryFor))
        {
            await CancelTaskTokenAsync(instance.Id, be.BoundaryFor, now, ct);

            // Отменяем также незавершённые задания хост-задачи
            var pendingJobs = await _db.BpmExecutionJobs
                .Where(j => j.InstanceId == instance.Id
                         && j.ElementId == be.BoundaryFor
                         && (j.Status == BpmJobStatus.Pending || j.Status == BpmJobStatus.Running))
                .ToListAsync(ct);

            foreach (var pj in pendingJobs)
            {
                pj.Status = BpmJobStatus.Cancelled;
                pj.UpdatedAt = now;
            }
        }

        // Создаём активный токен граничного события
        await CreateOrUpdateTokenAsync(instance.Id, be.Id, be.ElementType, be.Name,
            BpmTokenStatus.Active, now, ct);

        _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            EventType = BpmHistoryEventType.NodeExecuted,
            ElementId = be.Id,
            ElementName = be.Name,
            Text = $"Граничное таймерное событие «{be.Name ?? be.Id}» сработало",
            OccurredAt = now,
        });
        await _db.SaveChangesAsync(ct);

        // Продвигаем поток от граничного события
        await AdvanceFromAsync(instance.Id, be.Id, ct);
        return true;
    }

    /// <summary>
    /// Ищет граничное событие ошибки, прикреплённое к задаче, и активирует его если код ошибки совпадает.
    /// Возвращает true если граничное событие было активировано.
    /// </summary>
    private async Task<bool> TryActivateBoundaryErrorEventAsync(
        BpmInstance instance,
        string taskElementId,
        string? errorMessage,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var model = await ParseModelAsync(instance.ProcessVersionId, ct);
        if (model == null) return false;

        // Получаем конфиг задачи для BoundaryErrorCode из ErrorPolicyTab
        var configJson = await _db.BpmElementConfigs
            .AsNoTracking()
            .Where(c => c.ProcessId == instance.ProcessId && c.ElementId == taskElementId)
            .Select(c => c.ConfigJson)
            .FirstOrDefaultAsync(ct);

        string? configuredBoundaryErrorCode = null;
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                using var cfgDoc = System.Text.Json.JsonDocument.Parse(configJson);
                if (cfgDoc.RootElement.TryGetProperty("boundaryErrorCode", out var bec))
                    configuredBoundaryErrorCode = bec.GetString();
            }
            catch { /* игнорируем */ }
        }

        // Ищем boundaryEvent прикреплённые к задаче
        var boundaryEvents = model.AllElements
            .Where(e => e.ElementType == "boundaryEvent" && e.BoundaryFor == taskElementId)
            .ToList();

        foreach (var be in boundaryEvents)
        {
            // Граничное событие ошибки
            if (!string.IsNullOrWhiteSpace(be.BoundaryErrorCode) || configuredBoundaryErrorCode != null)
            {
                // Совпадение по коду ошибки или если код не задан (универсальный обработчик)
                var beErrorCode = be.BoundaryErrorCode ?? configuredBoundaryErrorCode;
                var isMatch = string.IsNullOrWhiteSpace(beErrorCode) ||
                              errorMessage?.Contains(beErrorCode, StringComparison.OrdinalIgnoreCase) == true;

                if (!isMatch) continue;

                _logger.LogInformation(
                    "Граничное событие ошибки {BoundaryId} активировано для задачи {TaskId} (ошибка: {Error})",
                    be.Id, taskElementId, SanitizeForLog(errorMessage));

                // Отменяем хост-задачу (если cancelActivity=true — по умолчанию true)
                if (be.IsCancelActivity != false)
                    await CancelTaskTokenAsync(instance.Id, taskElementId, now, ct);

                // Создаём токен граничного события
                await CreateOrUpdateTokenAsync(instance.Id, be.Id, be.ElementType, be.Name,
                    BpmTokenStatus.Active, now, ct);

                _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    EventType = BpmHistoryEventType.NodeExecuted,
                    ElementId = be.Id,
                    ElementName = be.Name,
                    Text = $"Граничное событие ошибки «{be.Name ?? be.Id}» активировано",
                    OccurredAt = now,
                });
                await _db.SaveChangesAsync(ct);

                // Продвигаем поток от граничного события
                await AdvanceFromAsync(instance.Id, be.Id, ct);
                return true;
            }
        }

        return false;
    }

    /// <summary>Прерывает активный токен задачи при срабатывании прерывающего граничного события.</summary>
    private async Task CancelTaskTokenAsync(Guid instanceId, string taskElementId, DateTimeOffset now, CancellationToken ct)
    {
        var taskToken = await _db.BpmTokens
            .FirstOrDefaultAsync(t => t.InstanceId == instanceId &&
                                      t.ElementId == taskElementId &&
                                      t.Status != BpmTokenStatus.Completed, ct);
        if (taskToken != null)
        {
            taskToken.Status = BpmTokenStatus.Completed;
            taskToken.CompletedAt = now;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Обновляет счётчик схождения AND-join шлюза.
    /// Возвращает true, если все входящие ветки пришли (можно продолжить выполнение).
    /// </summary>
    private async Task<bool> HandleJoinCounterAsync(
        Guid instanceId,
        string gatewayElementId,
        int expectedCount,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Атомарно инкрементируем счётчик (без read-modify-write гонки)
        var updatedRows = await _db.BpmJoinCounters
            .Where(c => c.InstanceId == instanceId && c.GatewayElementId == gatewayElementId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.ArrivedCount, c => c.ArrivedCount + 1)
                .SetProperty(c => c.UpdatedAt, now), ct);

        if (updatedRows == 0)
        {
            // Счётчик ещё не существует — создаём
            var counter = new BpmJoinCounter
            {
                Id = Guid.NewGuid(),
                InstanceId = instanceId,
                GatewayElementId = gatewayElementId,
                ExpectedCount = expectedCount,
                ArrivedCount = 1,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.BpmJoinCounters.Add(counter);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Конкурентная вставка — счётчик уже создан другим потоком, делаем инкремент
                _db.Entry(counter).State = EntityState.Detached;
                await _db.BpmJoinCounters
                    .Where(c => c.InstanceId == instanceId && c.GatewayElementId == gatewayElementId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.ArrivedCount, c => c.ArrivedCount + 1)
                        .SetProperty(c => c.UpdatedAt, now), ct);
            }
        }

        // Перечитываем актуальное значение счётчика
        var current = await _db.BpmJoinCounters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.InstanceId == instanceId && c.GatewayElementId == gatewayElementId, ct);

        if (current == null || current.ArrivedCount < current.ExpectedCount)
        {
            _logger.LogDebug(
                "AND-Join {GatewayId}: пришло {Arrived}/{Expected} — ждём",
                gatewayElementId, current?.ArrivedCount ?? 1, expectedCount);
            return false;
        }

        // Атомарное удаление: только один конкурентный поток получит deleted > 0 и продолжит
        var deletedRows = await _db.BpmJoinCounters
            .Where(c => c.InstanceId == instanceId && c.GatewayElementId == gatewayElementId)
            .ExecuteDeleteAsync(ct);

        if (deletedRows > 0)
        {
            _logger.LogInformation(
                "AND-Join {GatewayId}: все {Count} входящих токена прибыли — проходим", gatewayElementId, expectedCount);
            return true;
        }

        // Счётчик уже удалён другим потоком — не дублируем продвижение
        _logger.LogDebug(
            "AND-Join {GatewayId}: счётчик уже удалён конкурентным потоком — пропускаем", gatewayElementId);
        return false;
    }

    /// <summary>
    /// Определяет, является ли исключение нарушением unique-ограничения PostgreSQL.
    /// Проверка выполняется через SqlState ("23505") — не зависит от языковой локали сервера.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Обходим цепочку вложенных исключений
        Exception? inner = ex;
        while (inner != null)
        {
            if (inner is PostgresException pgEx)
                return pgEx.SqlState == "23505";  // unique_violation (не зависит от локали)
            inner = inner.InnerException;
        }
        return false;
    }

    /// <summary>
    /// Проверяет, завершились ли все активные токены экземпляра. Если да — помечает экземпляр как Completed.
    /// Вызывается когда элемент не имеет исходящих потоков.
    /// </summary>
    private async Task CheckAndCompleteInstanceIfFinishedAsync(
        BpmInstance instance,
        ProcessModel model,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var activeTokensCount = await _db.BpmTokens
            .CountAsync(t => t.InstanceId == instance.Id &&
                             t.Status != BpmTokenStatus.Completed, ct);

        if (activeTokensCount == 0)
        {
            var tracked = await _db.BpmInstances.FirstOrDefaultAsync(i => i.Id == instance.Id, ct);
            if (tracked != null && tracked.State == BpmInstanceState.Active)
            {
                tracked.State = BpmInstanceState.Completed;
                tracked.CompletedAt = now;
                tracked.UpdatedAt = now;

                _db.BpmInstanceHistoryEntries.Add(new BpmInstanceHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    InstanceId = instance.Id,
                    EventType = BpmHistoryEventType.Completed,
                    Text = "Процесс завершён (все токены исчерпаны)",
                    OccurredAt = now,
                });

                await _db.SaveChangesAsync(ct);
            }
        }
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

        ParseContainerElements(process, ns, elements, flows);

        var startEvents = elements.Where(e => e.ElementType == "startEvent").ToList();
        return new ProcessModel(versionId, elements, flows, startEvents);
    }

    /// <summary>
    /// Рекурсивно разбирает дочерние элементы BPMN-контейнера (process или subProcess).
    /// </summary>
    private static void ParseContainerElements(
        XElement container,
        XNamespace ns,
        List<ProcessElement> elements,
        List<SequenceFlowInfo> flows,
        string? parentSubProcessId = null)
    {
        foreach (var el in container.Elements())
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
                    var condEl = el.Element(ns + "conditionExpression")
                               ?? el.Element("conditionExpression");
                    var condition = condEl?.Value?.Trim();
                    flows.Add(new SequenceFlowInfo(id, sourceRef, targetRef, condition));
                }
            }
            else if (localName == "subProcess")
            {
                // Добавляем subProcess как элемент и рекурсивно обрабатываем вложенные
                elements.Add(new ProcessElement(id, localName, name, null, null,
                    ContainerElementId: parentSubProcessId));
                ParseContainerElements(el, ns, elements, flows, id);
            }
            else if (localName == "callActivity")
            {
                var calledElement = el.Attribute("calledElement")?.Value;
                elements.Add(new ProcessElement(id, localName, name, null, null,
                    CalledElement: calledElement,
                    ContainerElementId: parentSubProcessId));
            }
            else if (localName == "boundaryEvent")
            {
                // Граничные события
                var attachedToRef = el.Attribute("attachedToRef")?.Value;
                var cancelActivity = el.Attribute("cancelActivity")?.Value;
                var isCancelActivity = cancelActivity == null || cancelActivity != "false";

                string? signalCode = null;
                string? messageCode = null;
                string? timerDuration = null;
                string? timerCycle = null;
                string? timerDate = null;
                string? errorCode = null;

                var errorDef = el.Descendants(ns + "errorEventDefinition")
                    .Concat(el.Descendants("errorEventDefinition")).FirstOrDefault();
                if (errorDef != null)
                {
                    errorCode = errorDef.Attribute("errorRef")?.Value
                             ?? errorDef.Attribute("errorCode")?.Value;
                }

                var timerDef = el.Descendants(ns + "timerEventDefinition")
                    .Concat(el.Descendants("timerEventDefinition")).FirstOrDefault();
                if (timerDef != null)
                {
                    timerDuration = timerDef.Element(ns + "timeDuration")?.Value?.Trim()
                                 ?? timerDef.Element("timeDuration")?.Value?.Trim();
                    timerCycle = timerDef.Element(ns + "timeCycle")?.Value?.Trim()
                              ?? timerDef.Element("timeCycle")?.Value?.Trim();
                    timerDate = timerDef.Element(ns + "timeDate")?.Value?.Trim()
                             ?? timerDef.Element("timeDate")?.Value?.Trim();
                }

                var signalRef = el.Descendants(ns + "signalEventDefinition")
                    .Concat(el.Descendants("signalEventDefinition"))
                    .FirstOrDefault()?.Attribute("signalRef")?.Value;
                if (signalRef != null) signalCode = signalRef;

                var msgRef = el.Descendants(ns + "messageEventDefinition")
                    .Concat(el.Descendants("messageEventDefinition"))
                    .FirstOrDefault()?.Attribute("messageRef")?.Value;
                if (msgRef != null) messageCode = msgRef;

                elements.Add(new ProcessElement(
                    id, localName, name,
                    signalCode, messageCode,
                    timerDuration, timerCycle, timerDate,
                    attachedToRef, isCancelActivity, errorCode,
                    ContainerElementId: parentSubProcessId));
            }
            else
            {
                // Определяем тип события (signal/message/timer)
                string? signalCode = null;
                string? messageCode = null;
                string? timerDuration = null;
                string? timerCycle = null;
                string? timerDate = null;

                if (localName.Contains("Event", StringComparison.OrdinalIgnoreCase))
                {
                    var signalRef = el.Descendants(ns + "signalEventDefinition")
                                     .Concat(el.Descendants("signalEventDefinition"))
                                     .FirstOrDefault()?.Attribute("signalRef")?.Value;
                    if (signalRef != null) signalCode = signalRef;

                    var msgRef = el.Descendants(ns + "messageEventDefinition")
                                   .Concat(el.Descendants("messageEventDefinition"))
                                   .FirstOrDefault()?.Attribute("messageRef")?.Value;
                    if (msgRef != null) messageCode = msgRef;

                    var timerDef = el.Descendants(ns + "timerEventDefinition")
                        .Concat(el.Descendants("timerEventDefinition")).FirstOrDefault();
                    if (timerDef != null)
                    {
                        timerDuration = timerDef.Element(ns + "timeDuration")?.Value?.Trim()
                                     ?? timerDef.Element("timeDuration")?.Value?.Trim();
                        timerCycle = timerDef.Element(ns + "timeCycle")?.Value?.Trim()
                                  ?? timerDef.Element("timeCycle")?.Value?.Trim();
                        timerDate = timerDef.Element(ns + "timeDate")?.Value?.Trim()
                                 ?? timerDef.Element("timeDate")?.Value?.Trim();
                    }
                }

                elements.Add(new ProcessElement(id, localName, name, signalCode, messageCode,
                    timerDuration, timerCycle, timerDate,
                    ContainerElementId: parentSubProcessId));
            }
        }
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
        new(t.Id, t.InstanceId, t.ElementId, t.ElementType, t.ElementName, t.Status, t.SignalCode, t.MessageCode, t.CreatedAt, t.CompletedAt, t.LinkedTaskItemId);

    /// <summary>
    /// Санирует строку для логирования: убирает переводы строк, ограничивает длину.
    /// Защита от log-forging при использовании пользовательских данных в логах.
    /// </summary>
    private static string SanitizeForLog(string? value, int maxLen = 100)
    {
        if (value is null) return "(null)";
        return value.Replace("\r", "").Replace("\n", "")[..Math.Min(value.Length, maxLen)];
    }

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
        string? MessageCode,
        /// <summary>ISO 8601 длительность таймера (PT5M, P1D и т.д.).</summary>
        string? TimerDuration = null,
        /// <summary>Cron-выражение для repeating timer.</summary>
        string? TimerCycle = null,
        /// <summary>Конкретная дата-время срабатывания ISO 8601.</summary>
        string? TimerDate = null,
        /// <summary>Идентификатор элемента, к которому прикреплено граничное событие (attachedToRef).</summary>
        string? BoundaryFor = null,
        /// <summary>Прерывает ли граничное событие хост-задачу (cancelActivity).</summary>
        bool? IsCancelActivity = null,
        /// <summary>Код ошибки в boundaryEvent с errorEventDefinition (errorRef или errorCode).</summary>
        string? BoundaryErrorCode = null,
        /// <summary>Ссылка на вызываемый процесс (calledElement) в callActivity.</summary>
        string? CalledElement = null,
        /// <summary>Id родительского subProcess-элемента (для вложенных узлов embedded subprocess).</summary>
        string? ContainerElementId = null);

    private record SequenceFlowInfo(
        string Id,
        string SourceRef,
        string TargetRef,
        string? ConditionExpression);

    /// <summary>Маппинг переменной CallActivity (входной или выходной).</summary>
    private record VariableMapping(
        [property: System.Text.Json.Serialization.JsonPropertyName("sourceVar")] string SourceVar,
        [property: System.Text.Json.Serialization.JsonPropertyName("targetVar")] string TargetVar);
}
